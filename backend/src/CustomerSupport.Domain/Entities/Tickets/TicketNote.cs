namespace CustomerSupport.Domain.Entities.Tickets;

public class TicketNote : BaseEntity
{
    public Guid TicketId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public bool IsInternal { get; private set; }

    public static TicketNote Create(Guid ticketId, string body, bool isInternal, Guid createdBy)
    {
        if (ticketId == Guid.Empty)
            throw new ArgumentException("A ticket is required", nameof(ticketId));

        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required", nameof(body));

        if (body.Length > 4000)
            throw new ArgumentException("Body must not exceed 4000 characters", nameof(body));

        return new TicketNote
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Body = body.Trim(),
            IsInternal = isInternal,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }
}
