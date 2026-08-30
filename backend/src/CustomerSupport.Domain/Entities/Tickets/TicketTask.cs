namespace CustomerSupport.Domain.Entities.Tickets;

public class TicketTask : BaseEntity
{
    public Guid TicketId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTime? DueAt { get; private set; }
    public bool IsDone { get; private set; }

    public static TicketTask Create(Guid ticketId, string title, DateTime? dueAt, Guid createdBy)
    {
        if (ticketId == Guid.Empty)
            throw new ArgumentException("A ticket is required", nameof(ticketId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        if (title.Length > 200)
            throw new ArgumentException("Title must not exceed 200 characters", nameof(title));

        return new TicketTask
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Title = title.Trim(),
            DueAt = dueAt,
            IsDone = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }

    public void ToggleDone()
    {
        IsDone = !IsDone;
        MarkUpdated();
    }

    public void Update(string title, DateTime? dueAt, Guid actorId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        if (title.Length > 200)
            throw new ArgumentException("Title must not exceed 200 characters", nameof(title));

        Title = title.Trim();
        DueAt = dueAt;
        MarkUpdated();
        UpdatedBy = actorId;
    }
}
