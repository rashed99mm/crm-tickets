using System.Text;
using Asp.Versioning;
using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;
using CustomerSupport.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>
/// FEAT-25 — Twilio's inbound SMS webhook (CC-40/CC-41). Anonymous by nature: Twilio posts without
/// any bearer it trusts, so authenticity rests entirely on <c>X-Twilio-Signature</c>, checked before
/// any database is touched. Structure follows <see cref="WhatsAppWebhookController"/>; the
/// differences are the signature scheme (Twilio signs the URL plus sorted parameters, HMAC-SHA1,
/// Base64) and the payload shape (form-encoded, not JSON).
/// </summary>
[ApiController]
[Route("api/channels/sms")]
[ApiVersion("1.0")]
public class SmsWebhookController(
    IMediator mediator,
    IWebhookSignatureVerifier verifier,
    ILogger<SmsWebhookController> logger)
    : ControllerBase
{
    private const string SignatureHeader = "X-Twilio-Signature";

    /// <summary>
    /// Receives an inbound SMS. Answers 200 for any authentic delivery regardless of the downstream
    /// outcome — a failed ingestion is not a retryable webhook, and Twilio would otherwise redeliver
    /// it for hours. Unsigned or mismatched deliveries are refused with 401 before any database is
    /// touched (CC-41); an authentic delivery carrying no message (a delivery-status callback to the
    /// same URL) is 400.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        if (!Request.HasFormContentType)
        {
            logger.LogWarning("SMS webhook refused: not a form post");
            return BadRequest();
        }

        // Unlike the WhatsApp webhook, this one cannot read the raw body: MVC's form value provider
        // has already read and cached it during model binding, leaving the stream drained (verified
        // — the body is 0 bytes here while Request.Form is fully populated). That is fine for this
        // provider and only this provider: Twilio signs the *decoded* parameter values, so
        // re-encoding the framework's parsed form is lossless for the check. Meta's scheme, which
        // hashes the original bytes, could not be verified this way.
        var form = await Request.ReadFormAsync(ct);
        var canonicalBody = Encoding.UTF8.GetBytes(string.Join("&", form.Select(field =>
            $"{Uri.EscapeDataString(field.Key)}={Uri.EscapeDataString(field.Value.ToString())}")));

        Request.Headers.TryGetValue(SignatureHeader, out var signature);

        // Twilio signs the URL it was configured to post to, so the check needs the URL as this
        // request arrived, not just the parameters.
        if (!verifier.Verify(ChannelNames.Sms, signature.ToString(), Request.GetDisplayUrl(), canonicalBody))
        {
            logger.LogWarning("SMS webhook refused: invalid signature ({Fields} fields)", form.Count);
            return Unauthorized();
        }

        var from = form["From"].ToString();
        var body = form["Body"].ToString();
        var messageSid = form["MessageSid"].ToString();

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(body))
        {
            // Authentic but not an inbound message — Twilio posts delivery-status callbacks here too.
            logger.LogWarning("SMS webhook refused: no ingestible message (sid {Sid})", messageSid);
            return BadRequest();
        }

        await mediator.Send(new IngestInboundChannelMessageCommand(
            Channel: ChannelNames.Sms,
            CustomerName: null,
            CustomerPhone: from,
            CustomerEmail: null,
            Body: body,
            ProviderMessageId: string.IsNullOrWhiteSpace(messageSid) ? null : messageSid), ct);

        return Ok();
    }
}
