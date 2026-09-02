namespace CustomerSupport.Application.Channels;

/// <summary>
/// A normalized inbound message from any external channel, before it becomes a Ticket/TicketMessage.
/// Each provider webhook controller parses its own payload shape into this, so the ingestion command
/// downstream is channel-agnostic.
/// </summary>
public sealed record InboundChannelMessage(
    string Channel,             // one of CustomerSupport.Domain.Common.ChannelNames.Inbound (CC-48)
    string? CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string Body,
    string? ProviderMessageId,
    DateTime ReceivedAt);

/// <summary>
/// Verifies the signature an external provider attaches to webhook deliveries. Providers are
/// different enough (Meta: raw-body HMAC-SHA256; Twilio: URL + form-params HMAC-SHA1) that the
/// algorithm is per-provider, but every check must run against the untouched byte stream before any
/// model binding reformats it (CC-5/CC-27).
///
/// A pure port — no ASP.NET types — so the Application layer never depends on the web framework.
/// The webhook controllers (who own the <c>HttpRequest</c>) extract the signature header, the
/// request URL and the raw body, and pass the three primitives in.
/// </summary>
public interface IWebhookSignatureVerifier
{
    /// <param name="provider">"WhatsApp" or "SMS" — resolves which secret/algorithm to use.</param>
    /// <param name="signature">The raw signature header value sent by the provider
    /// (<c>X-Hub-Signature-256</c>, <c>X-Twilio-Signature</c>, ...).</param>
    /// <param name="requestUrl">The full request URL as the provider would have seen it when signing
    /// (Twilio signs URL + form parameters; Meta ignores it and may be null).</param>
    /// <param name="rawBody">The exact bytes received, before any model binding touches them.</param>
    bool Verify(string provider, string? signature, string? requestUrl, byte[] rawBody);
}