using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Entities.Ai;

/// <summary>Which surface a conversation belongs to (spec A5) — the scoping half of AI-40.</summary>
public enum AiChatScope
{
    Staff = 0,
    Portal = 1
}

public enum AiChatStatus
{
    Open = 0,
    Closed = 1
}

/// <summary>
/// One multi-turn grounded conversation (AI-38). A session belongs to exactly one actor on one
/// surface: an id from the portal scope can never be resolved through the staff host and vice
/// versa, which is what makes AI-40's safe not-found enforceable without trusting the client.
/// </summary>
public class AiChatSession : BaseEntity
{
    public Guid ActorId { get; private set; }
    public AiChatScope Scope { get; private set; }
    public AiChatStatus Status { get; private set; }

    /// <summary>Set by the handoff flow once a ticket was created from this conversation (AI-42).</summary>
    public Guid? TicketId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }

    public virtual ICollection<AiChatMessage> Messages { get; private set; } = new List<AiChatMessage>();

    private AiChatSession() { }

    public static AiChatSession Create(Guid actorId, AiChatScope scope)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("An actor is required", nameof(actorId));
        }

        return new AiChatSession
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            Scope = scope,
            Status = AiChatStatus.Open,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorId,
        };
    }

    public bool BelongsTo(Guid actorId, AiChatScope scope) =>
        ActorId == actorId && Scope == scope;

    /// <summary>Closing is terminal; a closed conversation stops accepting turns.</summary>
    public void Close()
    {
        if (Status == AiChatStatus.Closed)
        {
            return;
        }

        Status = AiChatStatus.Closed;
        ClosedAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }

    public void AttachTicket(Guid ticketId)
    {
        TicketId = ticketId;
        Close();
    }
}
