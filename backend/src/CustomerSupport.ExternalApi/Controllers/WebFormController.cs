using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Channels.Commands.SubmitWebForm;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>
/// FEAT-27 — the customer portal's web form (CC-20..CC-23, CC-47 as revised). The caller is
/// portal-app's own <c>web-form</c> feature, not a simulator (spec A20).
///
/// Anonymous by design: this is the intake surface for a visitor with no account. The honeypot and
/// rate-window defences live in <see cref="SubmitWebFormCommandHandler"/> — they are policy, and a
/// controller here binds, dispatches and maps, nothing more.
/// </summary>
[ApiController]
[Route("api/external/webform")]
[ApiVersion("1.0")]
public class WebFormController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Accepts a submission. A valid one creates (or appends to) the customer's open web-form
    /// ticket and returns its real reference; a honeypot-filled or throttled one answers
    /// identically with a reference that belongs to nothing, so a caller cannot tell the three
    /// apart (CC-47). A validation failure is a genuine 400 — the customer's own typo to fix.
    /// </summary>
    [HttpPost("submit")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Response<WebFormSubmissionResult>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<WebFormSubmissionResult>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(
        [FromBody] WebFormSubmissionRequest request, CancellationToken ct)
    {
        // The client key is read from the connection, never from the payload: rate limiting a value
        // the caller chooses limits nobody.
        var result = await mediator.Send(
            new SubmitWebFormCommand(
                request.Name,
                request.Email,
                request.Subject,
                request.Description,
                request.Honeypot,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous"),
            ct);

        return this.ToActionResult(result, StatusCodes.Status201Created);
    }
}
