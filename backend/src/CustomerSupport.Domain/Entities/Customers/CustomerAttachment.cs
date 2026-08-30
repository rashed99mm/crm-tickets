namespace CustomerSupport.Domain.Entities.Customers;

/// <summary>
/// An ownership link between a customer and a stored file. Carries no file metadata at all — that
/// lives in <c>Assets</c>, the single catalogue, so a future <c>TicketAttachments</c> reuses it
/// rather than altering it.
/// </summary>
public class CustomerAttachment : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public Guid AssetId { get; private set; }

    public static CustomerAttachment Create(Guid customerId, Guid assetId)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A customer is required", nameof(customerId));
        }

        if (assetId == Guid.Empty)
        {
            throw new ArgumentException("An asset is required", nameof(assetId));
        }

        return new CustomerAttachment
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            AssetId = assetId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
