using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Auth.Commands.ChangePassword;

/// <summary>
/// Changes the current user's password and revokes every refresh token belonging to
/// them, so a token issued before the change stops working. The access token already in
/// the caller's hand keeps working until it expires — access tokens are not revocable in
/// this design.
/// </summary>
public class ChangePasswordCommandHandler : ICommandHandler<ChangePasswordCommand, Response<Unit>>
{
    private readonly IIdentityUserService _identityUserService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IMessageFactory _messages;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IIdentityUserService identityUserService,
        IRefreshTokenService refreshTokenService,
        IMessageFactory messages,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _identityUserService = identityUserService;
        _refreshTokenService = refreshTokenService;
        _messages = messages;
        _logger = logger;
    }

    /// <summary>
    /// Handles the change-password command.
    /// </summary>
    /// <param name="request">The current and new password. Never logged.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A success result, or a validation error keyed to the offending field.</returns>
    public async Task<Response<Unit>> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Processing password change for user {UserId}", request.UserId);

        var user = await _identityUserService.FindByIdAsync(request.UserId, ct);
        if (user == null)
        {
            _logger.LogWarning("Password change failed — user {UserId} not found", request.UserId);
            return _messages.Fail<Unit>(ApplicationErrors.Auth.NOT_AUTHENTICATED, MessageType.Unauthorized);
        }

        var result = await _identityUserService.ChangePasswordAsync(
            user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            var isWrongCurrentPassword = result.ErrorCodes.Contains("PasswordMismatch");

            var (key, field) = isWrongCurrentPassword
                ? (ApplicationErrors.Auth.CURRENT_PASSWORD_INCORRECT, "currentPassword")
                : (ApplicationErrors.Auth.PASSWORD_TOO_WEAK, "newPassword");

            _logger.LogWarning(
                "Password change failed for user {UserId} — {Reason}",
                request.UserId,
                isWrongCurrentPassword ? "wrong current password" : "new password too weak");

            var fieldErrors = new List<FieldError>
            {
                new(field, SystemCodeMap.Resolve(key), key)
            };

            return _messages.Fail<Unit>(key, MessageType.Validation, fieldErrors);
        }

        await _refreshTokenService.RevokeAllUserRefreshTokensAsync(user.Id, null, ct);

        _logger.LogInformation("Password changed successfully for user {UserId}", request.UserId);

        return _messages.Success(Unit.Value, ApplicationErrors.Auth.PASSWORD_CHANGED);
    }
}
