namespace CustomerSupport.Domain.Entities.Organisation;

/// <summary>
/// Groups users, tickets and customers by location (AC-116). Grouping only this sprint — whether a
/// branch also restricts visibility is `OQ-5`, unresolved, and out of scope here (spec A1).
/// </summary>
public class Branch : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Region { get; private set; }
    public string Timezone { get; private set; } = "UTC";
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// <paramref name="id"/> is normally left to generate — the seeder is the one caller that needs
    /// a well-known id (AC-118), so a downstream feature can reference the default branch without a
    /// lookup.
    /// </summary>
    public static Branch Create(string name, string? region, string? timezone, Guid? id = null)
    {
        return new Branch
        {
            Id = id ?? Guid.NewGuid(),
            Name = ValidateName(name),
            Region = Normalise(region),
            Timezone = ValidateTimezone(timezone),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? region, string? timezone)
    {
        Name = ValidateName(name);
        Region = Normalise(region);
        Timezone = ValidateTimezone(timezone);
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

    private static string ValidateTimezone(string? timezone)
    {
        var value = string.IsNullOrWhiteSpace(timezone) ? "UTC" : timezone.Trim();

        if (value.Length > 100)
        {
            throw new ArgumentException("Timezone must not exceed 100 characters", nameof(timezone));
        }

        return value;
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
