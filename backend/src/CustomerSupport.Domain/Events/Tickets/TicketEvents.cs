namespace CustomerSupport.Domain.Events.Tickets;

/// <summary>Raised when a ticket is first recorded.</summary>
public sealed class TicketCreatedEvent(Guid ticketId, string reference, Guid customerId, Guid actorId)
    : DomainEvent
{
    public Guid TicketId { get; } = ticketId;
    public string Reference { get; } = reference;
    public Guid CustomerId { get; } = customerId;
    public Guid ActorId { get; } = actorId;
}

/// <summary>Raised when a ticket moves along its lifecycle, including a reopen.</summary>
public sealed class TicketStatusChangedEvent(Guid ticketId, string reference, string from, string to, Guid actorId)
    : DomainEvent
{
    public Guid TicketId { get; } = ticketId;
    public string Reference { get; } = reference;
    public string From { get; } = from;
    public string To { get; } = to;
    public Guid ActorId { get; } = actorId;
}

/// <summary>
/// Raised on assignment and on reassignment. <see cref="PreviousAssigneeId"/> is null for the
/// first, which is the same distinction history records as Assigned versus Reassigned.
/// </summary>
public sealed class TicketAssignedEvent(Guid ticketId, string reference, Guid? previousAssigneeId, Guid assigneeId, Guid actorId)
    : DomainEvent
{
    public Guid TicketId { get; } = ticketId;
    public string Reference { get; } = reference;
    public Guid? PreviousAssigneeId { get; } = previousAssigneeId;
    public Guid AssigneeId { get; } = assigneeId;
    public Guid ActorId { get; } = actorId;
}
