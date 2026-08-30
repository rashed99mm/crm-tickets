namespace CustomerSupport.Domain.Entities.Tickets;

/// <summary>
/// An ownership link between a ticket and a stored file. Carries no file metadata at all — that
/// lives in <c>Assets</c>, the single catalogue, so it is reused exactly as the analogous
/// <c>CustomerAttachment</c> link does (TA-4).
/// </summary>
public class TicketAttachment : BaseEntity
{
    public Guid TicketId { get; private set; }
    public Guid AssetId { get; private set; }

    public static TicketAttachment Create(Guid ticketId, Guid assetId)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A ticket is required", nameof(ticketId));
        }

        if (assetId == Guid.Empty)
        {
            throw new ArgumentException("An asset is required", nameof(assetId));
        }

        return new TicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AssetId = assetId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
