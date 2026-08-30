using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Auth.Commands.UpdateCurrentUserProfile;

/// <summary>
/// Updates the authenticated user's own profile and returns the refreshed <see cref="UserInfoDto"/>.
///
/// Authorization is by token only: the target is <see cref="IUserContext.UserId"/>, never a body
/// field, so a client cannot address another account (AC-430, AC-432). The domain
/// <c>ApplicationUser.UpdateProfile</c> touches exactly the four self-service columns; role, email,
/// username, active state, password, department and branch are out of reach (AC-432). A changed
/// phone is written back as unconfirmed; an unchanged phone keeps its existing confirmation state
/// (AC-436, AC-437).
/// </summary>
public class UpdateCurrentUserProfileCommandHandler
    : ICommandHandler<UpdateCurrentUserProfileCommand, Response<UserInfoDto>>
{
    private readonly IUserContext _userContext;
    private readonly IIdentityUserService _identityUserService;
    private readonly IMessageFactory _messages;
    private readonly ILogger<UpdateCurrentUserProfileCommandHandler> _logger;

    public UpdateCurrentUserProfileCommandHandler(
        IUserContext userContext,
        IIdentityUserService identityUserService,
        IMessageFactory messages,
        ILogger<UpdateCurrentUserProfileCommandHandler> logger)
    {
        _userContext = userContext;
        _identityUserService = identityUserService;
        _messages = messages;
        _logger = logger;
    }

    public async Task<Response<UserInfoDto>> Handle(UpdateCurrentUserProfileCommand request, CancellationToken ct)
    {
        if (!_userContext.IsAuthenticated || _userContext.UserId == Guid.Empty)
        {
            _logger.LogWarning("Profile update refused — not authenticated");
            return _messages.Fail<UserInfoDto>(ApplicationErrors.Auth.NOT_AUTHENTICATED, MessageType.Unauthorized);
        }

        var user = await _identityUserService.FindByIdAsync(_userContext.UserId, ct);
        if (user is null)
        {
            _logger.LogWarning("Profile update refused — user {UserId} not found", _userContext.UserId);
            return _messages.Fail<UserInfoDto>(ApplicationErrors.User.NOT_FOUND, MessageType.NotFound);
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Profile update refused — user {UserId} is inactive", _userContext.UserId);
            return _messages.Fail<UserInfoDto>(ApplicationErrors.Auth.ACCOUNT_DEACTIVATED, MessageType.Unauthorized);
        }

        var normalizedPhone = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();
        var phoneChanged = !string.Equals(user.PhoneNumber, normalizedPhone, StringComparison.Ordinal);

        // Only the four self-service fields are reachable here; nothing else can be bound or set.
        user.UpdateProfile(request.FirstName, request.LastName, normalizedPhone, request.ProfileImageUrl);

        // AC-436 / AC-437: a changed phone is stored as unconfirmed; an identical phone preserves its
        // existing confirmation state (no spurious reset).
        if (phoneChanged && user.PhoneNumber is not null)
        {
            user.PhoneNumberConfirmed = false;
        }

        var result = await _identityUserService.UpdateAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Profile update failed for user {UserId}", _userContext.UserId);
            return _messages.Fail<UserInfoDto>(ApplicationErrors.User.UPDATE_FAILED, MessageType.Validation);
        }

        _logger.LogInformation("Profile updated for user {UserId}", _userContext.UserId);

        var roles = await _identityUserService.GetRolesAsync(user);
        return _messages.Success(new UserInfoDto(
            user.Id,
            user.Email!,
            user.UserName!,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.EmailConfirmed,
            user.PhoneNumberConfirmed,
            user.IsActive,
            user.CreatedAt,
            roles.ToList()
        ), ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
