using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;

using CustomerSupport.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Auth.Commands.Logout;

/// <summary>
/// Revokes the refresh token to log out the current user.
/// </summary>
public class LogoutCommandHandler : ICommandHandler<LogoutCommand, Response<Unit>>
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IMessageFactory _messages;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(IRefreshTokenService refreshTokenService, IMessageFactory messages, ILogger<LogoutCommandHandler> logger)
    {
        _refreshTokenService = refreshTokenService;
        _messages = messages;
        _logger = logger;
    }

    /// <summary>
    /// Handles the logout command.
    /// </summary>
    /// <param name="request">The logout request containing the refresh token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A success result.</returns>
    public async Task<Response<Unit>> Handle(LogoutCommand request, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(request.RefreshToken))
        {
            _logger.LogInformation("Revoking refresh token");
            await _refreshTokenService.RevokeRefreshTokenAsync(request.RefreshToken, null, null, ct);
            _logger.LogInformation("Refresh token revoked successfully");
        }
        else
        {
            _logger.LogWarning("Logout requested without refresh token");
        }

        return _messages.Success(Unit.Value, ApplicationErrors.Auth.LOGOUT_SUCCESS);
    }
}
