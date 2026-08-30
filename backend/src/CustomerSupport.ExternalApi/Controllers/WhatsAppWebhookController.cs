using System.Text.Json;
using Asp.Versioning;
using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>
/// FEAT-24 — Meta's WhatsApp Business Cloud API webhook. Anonymous by nature (Meta posts without a
/// bearer it trusts), so the payload's authenticity rests entirely on <c>X-Hub-Signature-256</c> —
/// the signature is checked against the untouched raw body before anything else touches the stream.
/// Follows the KnowledgeBaseController convention: no class-level <c>[AllowAnonymous]</c>, the
/// attribute sits only on this action.
/// </summary>
[ApiController]
[Route("api/channels/whatsapp")]
[ApiVersion("1.0")]
public class WhatsAppWebhookController(
    IMediator mediator,
    IWebhookSignatureVerifier verifier,
    ILogger<WhatsAppWebhookController> logger)
    : ControllerBase
{
    private const string SignatureHeader = "X-Hub-Signature-256";

    /// <summary>
    /// Receives a webhook delivery. Returns 200 whenever the delivery is authentic, regardless of the
    /// downstream outcome — a failed ingestion is not a retryable webhook, and Meta would otherwise
    /// redeliver it forever. Unsigned or mismatched-signed deliveries are refused with 401 before any
    /// database is touched and before the payload is deserialized (CC-5).
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        var raw = ms.ToArray();
        Request.Body.Position = 0;

        Request.Headers.TryGetValue(SignatureHeader, out var signature);
        if (!verifier.Verify("WhatsApp", signature.ToString(), requestUrl: null, raw))
        {
            logger.LogWarning(
                "WhatsApp webhook refused: invalid signature (bytes: {Length})", raw.Length);
            return Unauthorized();
        }

        WhatsAppWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            logger.LogWarning("WhatsApp webhook refused: malformed JSON ({Bytes} bytes)", raw.Length);
            return BadRequest();
        }

        var message = SingleTextMessage(payload);
        if (message is null)
        {
            logger.LogWarning("WhatsApp webhook refused: no supported text message present");
            return BadRequest();
        }

        // CC-8 — the shared, channel-agnostic ingestion path. A retried delivery with the same
        // provider message id is a no-op at the database (CC-9), but still answered 200.
        await mediator.Send(new IngestInboundChannelMessageCommand(
            Channel: "WhatsApp",
            CustomerName: message.Value.Name,
            CustomerPhone: message.Value.From,
            CustomerEmail: null,
            Body: message.Value.Text,
            ProviderMessageId: message.Value.MessageId), ct);

        return Ok();
    }

    /// <summary>Flattens Meta's nested entry/changes/value/messages envelope to the one text message
    /// this channel can ingest. Returns null when the envelope holds nothing ingestible.</summary>
    private static (string From, string? Name, string MessageId, string Text)? SingleTextMessage(WhatsAppWebhookPayload? payload)
    {
        var changes = payload?.Entry?.FirstOrDefault()?.Changes;
        var value = changes?.FirstOrDefault(c => c.Field == "messages")?.Value;
        var message = value?.Messages?.FirstOrDefault(m => m.Type == "text" && m.Text is not null);
        if (message is null || string.IsNullOrWhiteSpace(message.From) || string.IsNullOrWhiteSpace(message.Id))
        {
            return null;
        }

        var name = value.Contacts?.FirstOrDefault(c => c.WaId == message.From)?.Profile?.Name;
        return (message.From, name, message.Id, message.Text!.Body ?? string.Empty);
    }
}

/// <summary>Meta's webhook envelope, mapped only as far as this channel reads (<c>text.type</c>).
/// System.Text.Json binds these case-sensitively because Meta's names are already lowercase.</summary>
public sealed record WhatsAppWebhookPayload(WhatsAppWebhookEntry[] Entry);
public sealed record WhatsAppWebhookEntry(WhatsAppChanges[] Changes);
public sealed record WhatsAppChanges(WhatsAppValue Value, string Field);
public sealed record WhatsAppValue(WhatsAppContact[]? Contacts, WhatsAppMessage[]? Messages);
public sealed record WhatsAppContact([property: System.Text.Json.Serialization.JsonPropertyName("wa_id")] string WaId, WhatsAppProfile Profile);
public sealed record WhatsAppProfile(string Name);
public sealed record WhatsAppMessage(string From, string Id, string Type, WhatsAppText? Text);
public sealed record WhatsAppText(string Body);