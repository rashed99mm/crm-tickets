using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;

using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace CustomerSupport.Application.Features.Auth.Commands.Login;

/// <summary>
/// Authenticates a user with email and password, returning JWT access and refresh tokens.
/// </summary>
public class LoginCommandHandler : ICommandHandler<LoginCommand, Response<AuthResponse>>
{
    private readonly IIdentityUserService _identityUserService;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IMessageFactory _messages;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IIdentityUserService identityUserService,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IMessageFactory messages,
        ILogger<LoginCommandHandler> logger)
    {
        _identityUserService = identityUserService;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _messages = messages;
        _logger = logger;
    }

    /// <summary>
    /// Handles the login command.
    /// </summary>
    /// <param name="request">The login credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing auth tokens or a localized error.</returns>
    public async Task<Response<AuthResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Processing login attempt");

        var user = await _identityUserService.FindByEmailAsync(request.Email, ct);
        if (user == null)
        {
            _logger.LogWarning("Login failed — user not found");
            return _messages.Fail<AuthResponse>(ApplicationErrors.Auth.INVALID_CREDENTIALS, MessageType.Unauthorized);
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed — account deactivated for user {UserId}", user.Id);
            return _messages.Fail<AuthResponse>(ApplicationErrors.Auth.ACCOUNT_DEACTIVATED, MessageType.Forbidden);
        }

        var passwordValid = await _identityUserService.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            _logger.LogWarning("Login failed — invalid password for user {UserId}", user.Id);
            return _messages.Fail<AuthResponse>(ApplicationErrors.Auth.INVALID_CREDENTIALS, MessageType.Unauthorized);
        }

        var roles = await _identityUserService.GetRolesAsync(user);

        var additionalClaims = user.CustomerId is { } cid
            ? new[] { new Claim(AuthClaimTypes.CustomerId, cid.ToString()) }
            : null;

        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.Email!, roles, additionalClaims);
        var refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(
            user.Id, request.IpAddress, request.UserAgent, ct);

        user.RecordLogin();

        _logger.LogInformation("Login successful for user {UserId} with roles {Roles}", user.Id, roles);

        return _messages.Success(new AuthResponse(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            accessToken,
            refreshToken.Token,
            _tokenService.GetTokenExpiration(accessToken),
            refreshToken.ExpiresAt,
            roles.ToList()
        ), ApplicationErrors.Auth.LOGIN_SUCCESS);
    }
}
