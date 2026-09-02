using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Domain.Entities.Tickets;

/// <summary>
/// A pointer between two tickets (US-925) — never a merge. Stored once; the reading side decides
/// how to render direction. Cross-ticket guards (target exists, duplicate row, direct cycle) are
/// the handler's — this entity cannot see other tickets.
/// </summary>
public class TicketLink : BaseEntity
{
    public Guid SourceTicketId { get; private set; }
    public Guid TargetTicketId { get; private set; }
    public string LinkType { get; private set; } = string.Empty;

    public static TicketLink Create(Guid sourceTicketId, Guid targetTicketId, string linkType, Guid createdBy)
    {
        if (sourceTicketId == Guid.Empty)
        {
            throw new ArgumentException("A source ticket is required", nameof(sourceTicketId));
        }

        if (targetTicketId == Guid.Empty)
        {
            throw new ArgumentException("A target ticket is required", nameof(targetTicketId));
        }

        if (sourceTicketId == targetTicketId)
        {
            throw new ArgumentException("A ticket cannot be linked to itself", nameof(targetTicketId));
        }

        if (createdBy == Guid.Empty)
        {
            throw new ArgumentException("An actor is required", nameof(createdBy));
        }

        return new TicketLink
        {
            Id = Guid.NewGuid(),
            SourceTicketId = sourceTicketId,
            TargetTicketId = targetTicketId,
            LinkType = TicketLinkType.Create(linkType).Value,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }
}
