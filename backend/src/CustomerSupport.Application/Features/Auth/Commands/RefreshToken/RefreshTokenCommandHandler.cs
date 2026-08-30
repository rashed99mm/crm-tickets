using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Application.Interfaces;
using MediatR;
using CustomerSupport.Domain.Entities.Identity;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace CustomerSupport.Application.Features.Auth.Commands.RefreshToken;

/// <summary>
/// Refreshes JWT access and refresh tokens using a valid refresh token.
/// </summary>
public class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, Response<AuthResponse>>
{
    private readonly IIdentityUserService _identityUserService;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IMessageFactory _messages;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IIdentityUserService identityUserService,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IMessageFactory messages,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _identityUserService = identityUserService;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _messages = messages;
        _logger = logger;
    }

    /// <summary>
    /// Handles the token refresh command.
    /// </summary>
    /// <param name="request">The refresh token request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing new auth tokens or a localized error.</returns>
    public async Task<Response<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Processing token refresh");

        var userId = _tokenService.GetUserIdFromToken(request.AccessToken);
        if (userId == null)
        {
            _logger.LogWarning("Token refresh failed — invalid access token");
            return _messages.Fail<AuthResponse>(ApplicationErrors.Auth.INVALID_TOKEN, MessageType.Unauthorized);
        }

        var isValidRefresh = await _refreshTokenService.ValidateRefreshTokenAsync(userId.Value, request.RefreshTokenValue, ct);
        if (!isValidRefresh)
        {
            _logger.LogWarning("Token refresh failed — invalid refresh token for user {UserId}", userId.Value);
            return _messages.Fail<AuthResponse>(ApplicationErrors.Auth.INVALID_REFRESH_TOKEN, MessageType.Unauthorized);
        }

        var user = await _identityUserService.FindByIdAsync(userId.Value, ct);
        if (user == null)
        {
            _logger.LogWarning("Token refresh failed — user {UserId} not found", userId.Value);
            return _messages.Fail<AuthResponse>(ApplicationErrors.User.NOT_FOUND, MessageType.NotFound);
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Token refresh failed — account deactivated for user {UserId}", user.Id);
            return _messages.Fail<AuthResponse>(ApplicationErrors.Auth.ACCOUNT_DEACTIVATED, MessageType.Forbidden);
        }

        var roles = await _identityUserService.GetRolesAsync(user);

        var additionalClaims = user.CustomerId is { } cid
            ? new[] { new Claim(AuthClaimTypes.CustomerId, cid.ToString()) }
            : null;

        await _refreshTokenService.RevokeRefreshTokenAsync(
            request.RefreshTokenValue,
            null,
            userId.ToString(),
            ct);

        var newAccessToken = _tokenService.GenerateAccessToken(user.Id, user.Email!, roles, additionalClaims);
        var newRefreshToken = await _refreshTokenService.CreateRefreshTokenAsync(
            user.Id, request.IpAddress, request.UserAgent, ct);

        _logger.LogInformation("Token refresh successful for user {UserId}", user.Id);

        return _messages.Success(new AuthResponse(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            newAccessToken,
            newRefreshToken.Token,
            _tokenService.GetTokenExpiration(newAccessToken),
            newRefreshToken.ExpiresAt,
            roles.ToList()
        ), ApplicationErrors.Auth.TOKEN_REFRESHED);
    }
}
