using CustomerSupport.Domain.Entities;

namespace CustomerSupport.Domain.Entities.Content;

/// <summary>FEAT-11, AC-178..181 — an article linked as the solution to a ticket, for deflection
/// tracking. Deliberately NOT IAppendOnlyEntity, unlike this codebase's other link/history
/// tables: AC-180 requires an actual removal on unlink, which the append-only guard forbids —
/// there is no "the link happened but was later undone" fact this feature asks to keep.</summary>
public class ContentTicketLink : BaseEntity
{
    public Guid TicketId { get; private set; }
    public Guid ContentId { get; private set; }
    public Guid LinkedByAgentId { get; private set; }
    public DateTime LinkedAt { get; private set; }

    public static ContentTicketLink Create(Guid ticketId, Guid contentId, Guid linkedByAgentId) => new()
    {
        Id = Guid.NewGuid(),
        TicketId = ticketId,
        ContentId = contentId,
        LinkedByAgentId = linkedByAgentId,
        LinkedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
    };
}
