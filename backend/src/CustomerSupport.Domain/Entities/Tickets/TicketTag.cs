using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Domain.Entities.Tickets;

/// <summary>
/// A free-form label on a ticket (US-924). Normalized at the door — a value that never passed
/// <see cref="TagValue.Normalize"/> cannot exist as a row.
/// </summary>
public class TicketTag : BaseEntity
{
    public Guid TicketId { get; private set; }
    public string Value { get; private set; } = string.Empty;

    public static TicketTag Create(Guid ticketId, string rawValue, Guid createdBy)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A ticket is required", nameof(ticketId));
        }

        if (createdBy == Guid.Empty)
        {
            throw new ArgumentException("An actor is required", nameof(createdBy));
        }

        return new TicketTag
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Value = TagValue.Normalize(rawValue),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }
}
