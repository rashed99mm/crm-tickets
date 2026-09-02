using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Entities.Tickets;

/// <summary>
/// One recorded communication against a ticket — a phone call, an email logged manually, an
/// internal note about contact made (AC-101). Distinct from <see cref="TicketHistory"/>: history
/// records *what happened to the ticket* (status, assignment); this records *what was said*.
///
/// <see cref="SenderId"/> is always the acting agent, even when <see cref="Direction"/> is
/// "Inbound" — customers have no login in this platform, so an inbound message this sprint means
/// an agent logging what a customer said, not a customer-authored record (spec A1).
/// </summary>
public class TicketMessage : BaseEntity, IAppendOnlyEntity
{
    private static readonly string[] AllowedDirections = ["Inbound", "Outbound"];
    private static readonly string[] AllowedChannels = ChannelNames.All;

    public Guid TicketId { get; private set; }
    public string Direction { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public string? Subject { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public Guid SenderId { get; private set; }

    /// <summary>
    /// The external provider's id for this message (WhatsApp message id, SMS message sid, ...).
    /// Null for messages recorded by an agent or by a channel that has no provider id. The
    /// partial-unique index on <c>(Channel, ProviderMessageId)</c> makes a retried webhook a
    /// no-op rather than a duplicate (CC-9/CC-12 idempotency).
    /// </summary>
    public string? ProviderMessageId { get; private set; }

    public DateTime SentAt { get; private set; }

    public static TicketMessage Create(
        Guid ticketId, string direction, string channel, string? subject, string body, Guid senderId,
        string? providerMessageId = null)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A ticket is required", nameof(ticketId));
        }

        if (!AllowedDirections.Contains(direction))
        {
            throw new ArgumentException($"Direction must be one of: {string.Join(", ", AllowedDirections)}", nameof(direction));
        }

        if (!AllowedChannels.Contains(channel))
        {
            throw new ArgumentException($"Channel must be one of: {string.Join(", ", AllowedChannels)}", nameof(channel));
        }

        if (subject is { Length: > 200 })
        {
            throw new ArgumentException("Subject must not exceed 200 characters", nameof(subject));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Body is required", nameof(body));
        }

        if (body.Length > 4000)
        {
            throw new ArgumentException("Body must not exceed 4000 characters", nameof(body));
        }

        if (senderId == Guid.Empty)
        {
            throw new ArgumentException("A sender is required", nameof(senderId));
        }

        return new TicketMessage
        {
            // Id deliberately unassigned — see TicketHistory.Record for why: a client-assigned Guid
            // on a row appended to an already-tracked Ticket makes EF mark it Modified, and the
            // append-only guard then refuses a perfectly legitimate append.
            TicketId = ticketId,
            Direction = direction,
            Channel = channel,
            Subject = subject,
            Body = body.Trim(),
            SenderId = senderId,
            ProviderMessageId = providerMessageId,
            SentAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = senderId
        };
    }
}
