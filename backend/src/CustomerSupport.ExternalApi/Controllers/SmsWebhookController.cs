using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Features.Channels.Commands.IngestTwilioSms;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>
/// FEAT-25 — Twilio's inbound SMS webhook (CC-40/CC-41). Anonymous by nature: Twilio posts without
/// any bearer it trusts, so authenticity rests entirely on <c>X-Twilio-Signature</c>.
///
/// The signature check and the payload's meaning belong to
/// <see cref="IngestTwilioSmsCommandHandler"/>. What is left here is binding — the form, the
/// signature header and the URL Twilio signed — and response mapping. A refused signature carries
/// <c>ERR067</c>, which <c>ToActionResult</c> already maps to 401, so there is nothing to branch on.
/// </summary>
[ApiController]
[Route("api/channels/sms")]
[ApiVersion("1.0")]
public class SmsWebhookController(IMediator mediator) : ControllerBase
{
    private const string SignatureHeader = "X-Twilio-Signature";

    /// <summary>
    /// Receives an inbound SMS. Answers 200 for any authentic delivery regardless of the
    /// downstream outcome — a failed ingestion is not a retryable webhook, and Twilio would
    /// otherwise redeliver it for hours. An unsigned or mismatched delivery is 401 with nothing
    /// written (CC-41); an authentic delivery carrying no message is 400.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);

        var result = await mediator.Send(
            new IngestTwilioSmsCommand(
                Form: form.ToDictionary(field => field.Key, field => field.Value.ToString()),
                Signature: Request.Headers.TryGetValue(SignatureHeader, out var signature)
                    ? signature.ToString()
                    : null,
                RequestUrl: Request.GetDisplayUrl()),
            ct);

        return this.ToActionResult(result);
    }
}
