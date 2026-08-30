using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;

using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Auth.Queries.GetCurrentUser;

/// <summary>
/// Retrieves the currently authenticated user's profile and roles.
/// </summary>
public class GetCurrentUserQueryHandler : IQueryHandler<GetCurrentUserQuery, Response<UserInfoDto>>
{
    private readonly IUserContext _userContext;
    private readonly IIdentityUserService _identityUserService;
    private readonly IMessageFactory _messages;
    private readonly ILogger<GetCurrentUserQueryHandler> _logger;

    public GetCurrentUserQueryHandler(IUserContext userContext, IIdentityUserService identityUserService, IMessageFactory messages, ILogger<GetCurrentUserQueryHandler> logger)
    {
        _userContext = userContext;
        _identityUserService = identityUserService;
        _messages = messages;
        _logger = logger;
    }

    /// <summary>
    /// Handles the get current user query.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the user info or a localized error.</returns>
    public async Task<Response<UserInfoDto>> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        if (!_userContext.IsAuthenticated || _userContext.UserId == Guid.Empty)
        {
            _logger.LogWarning("Get current user failed — user not authenticated");
            return _messages.Fail<UserInfoDto>(ApplicationErrors.Auth.NOT_AUTHENTICATED, MessageType.Unauthorized);
        }

        _logger.LogInformation("Retrieving current user {UserId}", _userContext.UserId);

        var user = await _identityUserService.FindByIdAsync(_userContext.UserId, ct);
        if (user == null)
        {
            _logger.LogWarning("Get current user failed — user {UserId} not found", _userContext.UserId);
            return _messages.Fail<UserInfoDto>(ApplicationErrors.User.NOT_FOUND, MessageType.NotFound);
        }

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
