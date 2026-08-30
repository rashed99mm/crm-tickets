using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Verification.Commands.RequestOtp;
using CustomerSupport.Application.Features.Verification.Commands.VerifyOtp;
using CustomerSupport.Application.Features.Verification.Dtos;
using CustomerSupport.Domain.Entities.Verification;
using CustomerSupport.Api.Shared.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Asp.Versioning;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// OTP contact verification. Authenticated staff only — there is deliberately no anonymous or
/// external route, because the consuming registration / recovery journeys live in a separate slice
/// (see spec A2). The caller is the token's user; the verification record's own user id scopes the
/// lookup, so a caller cannot verify or probe another account (AC-443).
/// </summary>
[ApiController]
[Route("api/verification")]
[ApiVersion("1.0")]
[Produces("application/json")]
public class VerificationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<VerificationController> _logger;

    public VerificationController(IMediator mediator, ILogger<VerificationController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Requests a fresh six-digit code for the caller's own contact. Body: <c>{ contact, type }</c>
    /// with <c>type</c> = <c>Email</c> or <c>Phone</c>. The code is dispatched through the matching
    /// notification channel (Email/SMS) and only persisted as a hash after the dispatch is accepted
    /// (OTP-1, OTP-2, OTP-9); a request inside the 60-second cooldown returns 429 without sending
    /// (OTP-3).
    /// </summary>
    [HttpPost("request")]
    [Authorize]
    [ProducesResponseType(typeof(Response<RequestOtpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Request([FromBody] RequestOtpCommand request, CancellationToken ct)
    {
        _logger.LogInformation("OTP request for {Type}", request.Type);
        var result = await _mediator.Send(request, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Requests a phone OTP for the caller's own phone number. Body: <c>{ phoneNumber }</c>. A
    /// convenience wrapper around <see cref="Request"/>; the profile screen uses it directly.
    /// </summary>
    [HttpPost("request-phone")]
    [Authorize]
    [ProducesResponseType(typeof(Response<RequestOtpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RequestPhone([FromBody] RequestPhoneVerificationRequest body, CancellationToken ct)
    {
        _logger.LogInformation("OTP phone request");
        var result = await _mediator.Send(
            new RequestOtpCommand(body.PhoneNumber, OtpVerificationType.Phone), ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Confirms a pending email or phone OTP. The body is <c>{ verificationId, code }</c>; the
    /// response never contains the code or its hash (AC-445).
    /// </summary>
    [HttpPost("verify")]
    [Authorize]
    [ProducesResponseType(typeof(Response<VerifyOtpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Verify([FromBody] VerifyOtpCommand request, CancellationToken ct)
    {
        _logger.LogInformation("OTP verification requested");
        var result = await _mediator.Send(request, ct);
        return this.ToActionResult(result);
    }
}

/// <summary>Body of <c>POST /api/verification/request-phone</c> — a single phone number.</summary>
public sealed record RequestPhoneVerificationRequest(string PhoneNumber);
