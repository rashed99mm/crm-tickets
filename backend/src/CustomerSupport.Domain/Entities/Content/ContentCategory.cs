using CustomerSupport.Domain.Entities;

namespace CustomerSupport.Domain.Entities.Content;

/// <summary>FEAT-11, AC-171/172/174. A hierarchical taxonomy for KB articles — deliberately its
/// own entity, not a reuse of the ticket `Category` (an unrelated, flat routing taxonomy) or of
/// `Content.Category`'s old free-text field, which this replaces.</summary>
public class ContentCategory : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static ContentCategory Create(string name, Guid? parentId, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required", nameof(name));
        }

        return new ContentCategory
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = Slugify(name),
            ParentId = parentId,
            SortOrder = sortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void Deactivate()
    {
        if (IsActive)
        {
            IsActive = false;
            MarkUpdated();
        }
    }

    private static string Slugify(string name) =>
        name.Trim().ToLowerInvariant().Replace(' ', '-');
}
