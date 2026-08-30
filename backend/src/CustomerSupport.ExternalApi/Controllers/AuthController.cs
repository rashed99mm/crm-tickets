using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Auth.Commands.Login;
using CustomerSupport.Application.Features.Auth.Commands.RefreshToken;
using CustomerSupport.Application.Features.Auth.Commands.Register;
using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Api.Shared.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Asp.Versioning;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>
/// Customer-facing authentication for the portal (sign up, sign in, token refresh).
///
/// The portal's dev proxy targets this host, and ADR-0008's narrow-surface rule is why these live
/// here rather than being borrowed from the staff host: a customer-facing deployment must carry
/// the endpoints its visitors need and nothing more — so this controller exposes only register,
/// login and refresh, and none of the staff surface (no <c>me</c>, no change-password, no logout
/// bookkeeping beyond what refresh requires).
/// </summary>
[ApiController]
[Route("api/Auth")]
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

    /// <summary>Creates a customer account and returns its id (ASG-8). The per-IP "login"
    /// window also guards registration against mass account creation.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Portal registration attempt received");

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
            userAgent,
            IsPortalRegistration: true
        );

        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result, StatusCodes.Status201Created);
    }

    /// <summary>Signs a customer in and returns JWT access and refresh tokens.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(Response<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Portal login attempt received");

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var command = new LoginCommand(request.Email, request.Password, ipAddress, userAgent);
        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result);
    }

    /// <summary>Refreshes the portal session's tokens.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Response<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
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
}
