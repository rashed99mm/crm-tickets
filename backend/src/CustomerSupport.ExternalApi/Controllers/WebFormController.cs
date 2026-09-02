using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;
using CustomerSupport.Application.Features.Tickets.Queries.GetTicketReferenceForMessage;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>
/// FEAT-27 — the customer portal's web form (CC-20..CC-23, CC-47 as revised). The caller is
/// portal-app's own <c>web-form</c> feature, not a simulator (spec A20), and the request/response
/// contract below is that screen's, already fixed:
/// <c>frontend/projects/common/src/lib/channels/web-form.api.ts</c>.
///
/// Anonymous by design — this is the intake surface for a visitor with no account — so the honeypot
/// and the throttle are the only defences, and CC-47 requires that neither be detectable from
/// outside: both answer exactly what a real submission answers.
/// </summary>
[ApiController]
[Route("api/external/webform")]
[ApiVersion("1.0")]
public class WebFormController(
    IMediator mediator,
    IWebFormSubmissionThrottle throttle,
    IMessageFactory messageFactory,
    ILogger<WebFormController> logger)
    : ControllerBase
{
    /// <summary>
    /// Accepts a submission. A valid one creates (or appends to) the customer's open web-form ticket
    /// and returns its real reference. A honeypot-filled or throttled one returns the same 201 with
    /// a plausible reference that belongs to nothing — a caller cannot distinguish the three
    /// (CC-47). Validation failures are genuine 400s: that is a customer fixing a typo, and the
    /// portal renders the field error.
    /// </summary>
    [HttpPost("submit")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Response<WebFormSubmissionResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<WebFormSubmissionResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromBody] WebFormSubmissionRequest request, CancellationToken ct)
    {
        // CC-22 — a populated honeypot is a bot: the field is hidden from real users. Answered
        // before the throttle so a bot cannot consume a human's budget.
        if (!string.IsNullOrWhiteSpace(request.Honeypot))
        {
            logger.LogInformation("Web-form submission discarded: honeypot populated");
            return PretendAccepted();
        }

        var clientKey = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        if (!throttle.TryAcquire(clientKey))
        {
            logger.LogInformation("Web-form submission discarded: client over its window budget");
            return PretendAccepted();
        }

        var ingested = await mediator.Send(new IngestInboundChannelMessageCommand(
            Channel: ChannelNames.WebForm,
            CustomerName: request.Name,
            CustomerPhone: null,
            CustomerEmail: request.Email,
            Body: request.Description,
            ProviderMessageId: null,
            Subject: request.Subject), ct);

        if (!ingested.Success)
        {
            return this.ToActionResult(ingested);
        }

        // A25 — the message id is what the shared command returns; the customer needs the ticket's
        // reference. One extra read, through MediatR like every other controller call.
        var reference = await mediator.Send(new GetTicketReferenceForMessageQuery(ingested.Data), ct);
        if (!reference.Success)
        {
            return this.ToActionResult(reference);
        }

        return StatusCode(
            StatusCodes.Status201Created,
            messageFactory.Success(
                new WebFormSubmissionResponse(reference.Data!, true),
                ApplicationErrors.Ticket.MESSAGE_RECORDED));
    }

    /// <summary>
    /// CC-47's indistinguishability requirement. The reference matches the real generator's
    /// TKT-nnnnnn shape (TicketReferenceGenerator.cs:49) but is drawn at random and never persisted,
    /// so it consumes no sequence value and resolves to no ticket.
    /// </summary>
    private IActionResult PretendAccepted() =>
        StatusCode(
            StatusCodes.Status201Created,
            messageFactory.Success(
                new WebFormSubmissionResponse($"TKT-{Random.Shared.Next(0, 1_000_000):D6}", true),
                ApplicationErrors.Ticket.MESSAGE_RECORDED));
}

/// <summary>
/// The portal's request shape, field-for-field (spec A20). <c>Honeypot</c> is optional and must
/// stay optional: the portal only sends it when its hidden input was filled.
/// </summary>
public sealed record WebFormSubmissionRequest(
    string Name,
    string Email,
    string Subject,
    string Description,
    string? Honeypot);

/// <summary>
/// Carried inside <c>Response&lt;T&gt;.Data</c>. portal-app's envelopeInterceptor
/// (<c>app.config.ts:23</c>) unwraps the envelope to <c>data</c>, so this is exactly what
/// <c>WebFormSubmissionResponse</c> in <c>web-form.api.ts</c> receives — including the nested
/// <c>success</c>, which that interface declares.
/// </summary>
public sealed record WebFormSubmissionResponse(string Reference, bool Success);
