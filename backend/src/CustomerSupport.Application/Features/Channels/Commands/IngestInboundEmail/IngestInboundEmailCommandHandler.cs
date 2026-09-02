using System.Net.Mail;
using System.Text.RegularExpressions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Channels.Commands.IngestInboundEmail;

/// <summary>
/// Turns SendGrid Inbound Parse's shape into the shared inbound path (CC-42/CC-43).
///
/// There is no signature to check, by design: Inbound Parse does not sign its posts, unlike
/// SendGrid's separate Event Webhook (spec A21). Nothing about the sender is authenticated beyond
/// what the payload claims — email is spoofable and this spec does not try to solve that.
///
/// See <see cref="SubmitWebForm.SubmitWebFormCommandHandler"/> for why a handler here dispatches the
/// shared ingestion command rather than re-implementing it.
/// </summary>
public partial class IngestInboundEmailCommandHandler(
    IMediator mediator,
    IMessageFactory messageFactory,
    ILogger<IngestInboundEmailCommandHandler> logger)
    : ICommandHandler<IngestInboundEmailCommand, Response<Guid>>
{
    /// <summary>The Message-ID line inside the forwarded raw headers — CC-43's idempotency key.</summary>
    [GeneratedRegex(@"^Message-ID:\s*(?<id>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex MessageIdHeader();

    public async Task<Response<Guid>> Handle(IngestInboundEmailCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.From) || string.IsNullOrWhiteSpace(request.Text))
        {
            logger.LogWarning("Inbound email refused: missing sender or empty body");
            return messageFactory.Fail<Guid>(
                ApplicationErrors.Channel.PAYLOAD_INVALID, MessageType.Validation);
        }

        if (!TryParseSender(request.From, out var address, out var displayName))
        {
            // Deliberately not logged in full: an inbound payload is untrusted content (CC-29).
            logger.LogWarning("Inbound email refused: From header could not be parsed");
            return messageFactory.Fail<Guid>(
                ApplicationErrors.Channel.PAYLOAD_INVALID, MessageType.Validation);
        }

        return await mediator.Send(
            new IngestInboundChannelMessageCommand(
                Channel: ChannelNames.Email,
                CustomerName: displayName,
                CustomerPhone: null,
                CustomerEmail: address,
                Body: request.Text,
                ProviderMessageId: ExtractMessageId(request.Headers),
                Subject: string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject),
            ct);
    }

    /// <summary>
    /// Splits <c>"Layla Haddad" &lt;layla@example.com&gt;</c> into address and display name.
    /// <see cref="MailAddress"/> is used rather than a regex because it already implements RFC
    /// 5322's quoting rules; a value it rejects is not an address this system could reply to, so it
    /// is refused rather than stored.
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

    /// <summary>Null when the sender omitted a Message-ID — the shared path then skips
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
