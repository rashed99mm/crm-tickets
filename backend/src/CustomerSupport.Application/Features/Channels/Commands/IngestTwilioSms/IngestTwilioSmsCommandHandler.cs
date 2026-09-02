using System.Text;
using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Channels.Commands.IngestTwilioSms;

/// <summary>
/// Verifies a Twilio delivery and hands it to the shared inbound path (CC-40/CC-41).
///
/// The signature is checked here, before anything is written, and a refusal maps to 401 through
/// <c>CHANNEL_WEBHOOK_SIGNATURE_INVALID</c>. Verification happens in a handler rather than the
/// controller so that "an unsigned delivery writes nothing" is a fact about the use case, provable
/// without an HTTP host, rather than a fact about one controller method.
///
/// See <see cref="SubmitWebForm.SubmitWebFormCommandHandler"/> for why this dispatches the shared
/// ingestion command instead of re-implementing it.
/// </summary>
public class IngestTwilioSmsCommandHandler(
    IMediator mediator,
    IWebhookSignatureVerifier verifier,
    IMessageFactory messageFactory,
    ILogger<IngestTwilioSmsCommandHandler> logger)
    : ICommandHandler<IngestTwilioSmsCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(IngestTwilioSmsCommand request, CancellationToken ct)
    {
        if (!verifier.Verify(
                ChannelNames.Sms, request.Signature, request.RequestUrl, CanonicalBody(request.Form)))
        {
            logger.LogWarning(
                "SMS webhook refused: invalid signature ({Fields} fields)", request.Form.Count);
            return messageFactory.Fail<Guid>(
                ApplicationErrors.Channel.WEBHOOK_SIGNATURE_INVALID, MessageType.BusinessRule);
        }

        var from = Field(request.Form, "From");
        var body = Field(request.Form, "Body");
        var messageSid = Field(request.Form, "MessageSid");

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(body))
        {
            // Authentic, but not an inbound message: Twilio posts delivery-status callbacks to the
            // same URL and they carry no Body.
            logger.LogWarning("SMS webhook refused: no ingestible message (sid {Sid})", messageSid);
            return messageFactory.Fail<Guid>(
                ApplicationErrors.Channel.PAYLOAD_INVALID, MessageType.Validation);
        }

        return await mediator.Send(
            new IngestInboundChannelMessageCommand(
                Channel: ChannelNames.Sms,
                // Twilio sends no display name, so a new customer is named by their number — the
                // behaviour spec A5 already defines for phone-matched channels.
                CustomerName: null,
                CustomerPhone: from,
                CustomerEmail: null,
                Body: body,
                ProviderMessageId: string.IsNullOrWhiteSpace(messageSid) ? null : messageSid),
            ct);
    }

    /// <summary>
    /// Re-encodes the parsed form as <c>application/x-www-form-urlencoded</c> for the verifier.
    ///
    /// The raw request body is not available by the time a controller action runs: MVC's form value
    /// provider reads and caches it during model binding, leaving the stream empty. That is fine
    /// for **this** provider and only this one — Twilio signs the *decoded* parameter values, so a
    /// decode/encode round trip is lossless. Meta's scheme hashes the original bytes and could not
    /// be verified this way, which is why the WhatsApp webhook still reads the raw stream.
    /// </summary>
    private static byte[] CanonicalBody(IReadOnlyDictionary<string, string> form) =>
        Encoding.UTF8.GetBytes(string.Join(
            "&", form.Select(f => $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value)}")));

    private static string Field(IReadOnlyDictionary<string, string> form, string key) =>
        form.TryGetValue(key, out var value) ? value : string.Empty;
}
