namespace CustomerSupport.Domain.Entities.Identity;

public sealed class Permission
{
    private Permission() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static Permission Create(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            throw new ArgumentException("Permission name must contain 1 to 100 characters.", nameof(name));
        }

        return new Permission
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
    }
}
