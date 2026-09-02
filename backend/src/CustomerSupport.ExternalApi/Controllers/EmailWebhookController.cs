using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Features.Channels.Commands.IngestInboundEmail;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>
/// FEAT-35 — inbound email in SendGrid Inbound Parse's shape (CC-42/CC-43).
///
/// Unlike the WhatsApp and SMS webhooks there is no signature to verify: Inbound Parse does not
/// sign its posts (spec A21). Parsing the <c>From</c> header and pulling the Message-ID out of the
/// forwarded headers is the handler's work, not this class's.
/// </summary>
[ApiController]
[Route("api/channels/email")]
[ApiVersion("1.0")]
public class EmailWebhookController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Receives a parsed inbound email. Answers 200 once the payload is ingestible regardless of
    /// the downstream outcome — SendGrid retries a non-2xx, and a message this system cannot
    /// process will not become processable on the third delivery. A payload with no usable sender
    /// or no body is 400.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Receive(
        [FromForm] SendGridInboundEmailRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new IngestInboundEmailCommand(
                request.From, request.Subject, request.Text, request.Headers),
            ct);

        // A refusal carries VAL001, which ToActionResult maps to 400 — nothing to branch on here.
        return this.ToActionResult(result);
    }
}
