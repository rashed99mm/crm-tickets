using System.Net.Mail;
using System.Text.RegularExpressions;
using Asp.Versioning;
using CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;
using CustomerSupport.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>
/// FEAT-35 — inbound email in SendGrid Inbound Parse's shape (CC-42/CC-43). Unlike the WhatsApp and
/// SMS webhooks there is **no signature to verify**: Inbound Parse does not sign its posts (spec
/// A21, and unlike SendGrid's separate Event Webhook, which does). Nothing about the sender is
/// therefore authenticated beyond what the payload itself claims — email is spoofable by design and
/// this spec does not try to solve that.
/// </summary>
[ApiController]
[Route("api/channels/email")]
[ApiVersion("1.0")]
public partial class EmailWebhookController(
    IMediator mediator,
    ILogger<EmailWebhookController> logger)
    : ControllerBase
{
    /// <summary>Inbound Parse forwards the original headers verbatim; the Message-ID line inside
    /// them is the only stable per-message id available for CC-43's idempotency.</summary>
    [GeneratedRegex(@"^Message-ID:\s*(?<id>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex MessageIdHeader();

    /// <summary>
    /// Receives a parsed inbound email. 200 once the payload is ingestible, regardless of the
    /// downstream outcome — SendGrid retries non-2xx responses, and a message this system cannot
    /// process will not become processable on the third delivery. A payload with no usable sender or
    /// no body is 400.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);

        var rawFrom = form["from"].ToString();
        var body = form["text"].ToString();
        var subject = form["subject"].ToString();

        if (string.IsNullOrWhiteSpace(rawFrom) || string.IsNullOrWhiteSpace(body))
        {
            logger.LogWarning("Inbound email refused: missing sender or empty body");
            return BadRequest();
        }

        if (!TryParseSender(rawFrom, out var address, out var displayName))
        {
            // Deliberately not logged in full: an inbound payload is untrusted content (CC-29).
            logger.LogWarning("Inbound email refused: From header could not be parsed");
            return BadRequest();
        }

        await mediator.Send(new IngestInboundChannelMessageCommand(
            Channel: ChannelNames.Email,
            CustomerName: displayName,
            CustomerPhone: null,
            CustomerEmail: address,
            Body: body,
            ProviderMessageId: ExtractMessageId(form["headers"].ToString()),
            Subject: string.IsNullOrWhiteSpace(subject) ? null : subject), ct);

        return Ok();
    }

    /// <summary>
    /// Splits <c>"Layla Haddad" &lt;layla@example.com&gt;</c> into its address and display name.
    /// MailAddress is used rather than a regex because it already implements RFC 5322's quoting
    /// rules; a value it rejects is not an address this system can reply to, so it is refused
    /// rather than stored.
    /// </summary>
    private static bool TryParseSender(string rawFrom, out string address, out string? displayName)
    {
        try
        {
            var parsed = new MailAddress(rawFrom.Trim());
            address = parsed.Address;
            displayName = string.IsNullOrWhiteSpace(parsed.DisplayName) ? null : parsed.DisplayName;
            return true;
        }
        catch (FormatException)
        {
            address = string.Empty;
            displayName = null;
            return false;
        }
        catch (ArgumentException)
        {
            address = string.Empty;
            displayName = null;
            return false;
        }
    }

    /// <summary>Null when the sender omitted a Message-ID — the shared handler then skips
    /// deduplication, exactly as it does for any channel with no provider id.</summary>
    private static string? ExtractMessageId(string? headers)
    {
        if (string.IsNullOrWhiteSpace(headers))
        {
            return null;
        }

        var match = MessageIdHeader().Match(headers);
        return match.Success ? match.Groups["id"].Value : null;
    }
}
