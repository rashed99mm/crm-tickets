namespace CustomerSupport.Domain.Entities.Organisation;

/// <summary>
/// Groups users, tickets and categories by organisational unit (AC-115). A lookup entity, the same
/// shape as <see cref="Tickets.Category"/>: an explicit <see cref="IsActive"/> flag toggled by
/// <see cref="Deactivate"/>, not the generic soft-delete flag — matching how this codebase already
/// deactivates a lookup row rather than reusing <c>Category</c>'s own soft-delete mechanism.
/// </summary>
public class Department : BaseEntity
{
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Unvalidated on purpose (spec A5): confirming this is a real user who may manage a department
    /// is an authorization design this story does not raise, and inventing one as a side effect of
    /// an unrelated entity would be scope nobody asked for.
    /// </summary>
    public Guid? ManagerId { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// <paramref name="id"/> is normally left to generate — the seeder is the one caller that needs
    /// a well-known id (AC-118), so a downstream feature can reference the default department
    /// without a lookup.
    /// </summary>
    public static Department Create(string name, Guid? managerId, Guid? id = null)
    {
        return new Department
        {
            Id = id ?? Guid.NewGuid(),
            Name = ValidateName(name),
            ManagerId = managerId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, Guid? managerId)
    {
        Name = ValidateName(name);
        ManagerId = managerId;
        MarkUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required", nameof(name));
        }

        if (name.Length > 200)
        {
            throw new ArgumentException("Name must not exceed 200 characters", nameof(name));
        }

        return name.Trim();
    }
}
