using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Messages;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>The API's own liveness, in the same envelope as everything else.</summary>
public record HealthDto(string Status, DateTime Timestamp);

/// <summary>
/// Provides a simple health check endpoint.
/// </summary>
/// <remarks>
/// Answers in the standard envelope (`AC-51`). It previously returned a bare
/// <c>{ status, timestamp }</c> object, which the contract-hardening audit caught as one of three
/// routes outside the envelope.
///
/// The minimal-API <c>GET /health</c> and <c>/health/ready</c> registered in
/// <c>MapPlatformEndpoints</c> are **deliberately not** wrapped: those are probe endpoints for
/// orchestrators and health-check tooling, which expect their own shape. This one is part of the
/// documented API surface, and a consumer parsing it should not need a special case.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class HealthController(ILogger<HealthController> logger) : ControllerBase
{
    /// <summary>Returns the current health status of the API.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(Response<HealthDto>), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        logger.LogDebug("Health check requested");

        return this.ToActionResult(
            Response<HealthDto>.Ok(new HealthDto("healthy", DateTime.UtcNow), SystemCodeMap.Resolve("HEALTHY"), "healthy"));
    }
}
