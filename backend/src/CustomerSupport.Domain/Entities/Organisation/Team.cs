namespace CustomerSupport.Domain.Entities.Organisation;

/// <summary>
/// Groups agents under a department (US-905, AC-508). The same shape as <see cref="Department"/>:
/// an explicit <see cref="IsActive"/> flag toggled by <see cref="Deactivate"/>. The drill-down
/// Org → Branch → Department → Team → Agent needs exactly this depth (spec A6: teams do not nest).
/// </summary>
public class Team : BaseEntity
{
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// The owning department. Confirming the manager is a real user is an authorization design this
    /// story does not raise, and inventing one as a side effect would be scope nobody asked for
    /// (same approach as <c>Department.ManagerId</c>).
    /// </summary>
    public Guid DepartmentId { get; private set; }

    public Guid? ManagerId { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// <paramref name="id"/> is normally left to generate — the seeder is the one caller that needs a
    /// well-known id so downstream features can reference the default team without a lookup.
    /// </summary>
    public static Team Create(string name, Guid departmentId, Guid? managerId, Guid? id = null)
    {
        if (departmentId == Guid.Empty)
        {
            throw new ArgumentException("A department is required", nameof(departmentId));
        }

        return new Team
        {
            Id = id ?? Guid.NewGuid(),
            Name = ValidateName(name),
            DepartmentId = departmentId,
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
