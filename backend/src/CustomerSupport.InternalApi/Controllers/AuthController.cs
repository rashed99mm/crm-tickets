using CustomerSupport.Application.Contracts;

using CustomerSupport.Application.Features.Auth.Commands.ChangePassword;
using CustomerSupport.Application.Features.Auth.Commands.Login;
using CustomerSupport.Application.Features.Auth.Commands.Logout;
using CustomerSupport.Application.Features.Auth.Commands.RefreshToken;
using CustomerSupport.Application.Features.Auth.Commands.Register;
using CustomerSupport.Application.Features.Auth.Commands.UpdateCurrentUserProfile;
using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Application.Features.Auth.Queries.GetCurrentUser;
using CustomerSupport.Api.Shared.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Asp.Versioning;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// Provides authentication endpoints for login, registration, token refresh, and logout.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IMediator mediator, ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and returns JWT access and refresh tokens.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(Response<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Login attempt received");

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var command = new LoginCommand(request.Email, request.Password, ipAddress, userAgent);
        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Registers a new user account.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Registration attempt received");

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var command = new RegisterCommand(
            request.Email,
            request.Username,
            request.Password,
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            ipAddress,
            userAgent
        );

        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result, StatusCodes.Status201Created);
    }

    /// <summary>
    /// Refreshes JWT tokens using a valid refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Response<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Token refresh attempt received");

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var command = new RefreshTokenCommand(
            request.AccessToken,
            request.RefreshToken,
            ipAddress,
            userAgent
        );

        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Logs out the current user by revoking the refresh token.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken ct)
    {
        _logger.LogInformation("Logout request received");

        var command = new LogoutCommand(request?.RefreshToken);
        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Retrieves the currently authenticated user's profile.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(Response<UserInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        _logger.LogInformation("Current user profile requested");

        var query = new GetCurrentUserQuery();
        var result = await _mediator.Send(query, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Changes the signed-in user's own password and revokes their other sessions.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Password change requested");

        var userId = User.GetRequiredUserId();
        var command = new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword);
        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Updates the signed-in user's own profile (first name, last name, phone number, profile image
    /// URL). The target is the authenticated user — no id is accepted in the body — and the response
    /// is the same <see cref="UserInfoDto"/> as <c>GET /api/Auth/me</c> (AC-430, AC-432, AC-446).
    /// </summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(Response<UserInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateCurrentUserProfile(
        [FromBody] UpdateCurrentUserProfileRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Profile update requested");

        var command = new UpdateCurrentUserProfileCommand(
            request.FirstName, request.LastName, request.PhoneNumber, request.ProfileImageUrl);
        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result);
    }
}
