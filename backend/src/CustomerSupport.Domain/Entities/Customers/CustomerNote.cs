namespace CustomerSupport.Domain.Entities.Customers;

/// <summary>
/// An internal note against a customer (AC-17..AC-21).
///
/// <see cref="AuthorId"/> is a required constructor argument with no setter and no default, so
/// there is no shape of this entity that carries an author supplied by a request body (AC-19).
/// The handler supplies it from the token.
/// </summary>
public class CustomerNote : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public Guid AuthorId { get; private set; }

    public static CustomerNote Create(Guid customerId, string body, Guid authorId)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A customer is required", nameof(customerId));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Body is required", nameof(body));
        }

        if (body.Length > 4000)
        {
            throw new ArgumentException("Body must not exceed 4000 characters", nameof(body));
        }

        if (authorId == Guid.Empty)
        {
            throw new ArgumentException("An author is required", nameof(authorId));
        }

        return new CustomerNote
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Body = body.Trim(),
            AuthorId = authorId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = authorId
        };
    }
}
