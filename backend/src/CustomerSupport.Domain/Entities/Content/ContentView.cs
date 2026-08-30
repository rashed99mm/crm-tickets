using CustomerSupport.Domain.Entities;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Entities.Content;

/// <summary>FEAT-11, AC-185/186 — one recorded view of a published article. Append-only: a view
/// is a fact about what happened, never edited or removed.</summary>
public class ContentView : BaseEntity, IAppendOnlyEntity
{
    public Guid ContentId { get; private set; }
    public Guid? UserId { get; private set; }
    public DateTime ViewedAt { get; private set; }

    public static ContentView Create(Guid contentId, Guid? userId) => new()
    {
        Id = Guid.NewGuid(),
        ContentId = contentId,
        UserId = userId,
        ViewedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
    };
}
