namespace CustomerSupport.Domain.Entities.Tickets;

/// <summary>
/// How a ticket is classified. A fixed seeded list, read-only in S1 (assumption A4) — free-text
/// categories are refused, so that reporting has something stable to group by.
/// </summary>
public class Category : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    /// <summary>Organisational grouping (FEAT-16, AC-117). Nullable and unset — see Ticket.DepartmentId.</summary>
    public Guid? DepartmentId { get; private set; }

    public static Category Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required", nameof(name));
        }

        if (name.Length > 100)
        {
            throw new ArgumentException("Name must not exceed 100 characters", nameof(name));
        }

        return new Category
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }
}
