using CustomerSupport.Domain.Entities;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Entities.Content;

/// <summary>One saved snapshot of an article — AC-168/169/170. Append-only: a version record is
/// never edited or removed after it's written, matching TicketHistory/SLAEvent's guard.</summary>
public class ContentVersion : BaseEntity, IAppendOnlyEntity
{
    public Guid ContentId { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid AuthorId { get; private set; }
    public string ChangeSummary { get; private set; } = string.Empty;
    public string TitleSnapshot { get; private set; } = string.Empty;
    public string BodySnapshot { get; private set; } = string.Empty;

    public static ContentVersion Create(Guid contentId, int versionNumber, Guid authorId,
        string changeSummary, string titleSnapshot, string bodySnapshot) => new()
    {
        Id = Guid.NewGuid(),
        ContentId = contentId,
        VersionNumber = versionNumber,
        AuthorId = authorId,
        ChangeSummary = changeSummary,
        TitleSnapshot = titleSnapshot,
        BodySnapshot = bodySnapshot,
        CreatedAt = DateTime.UtcNow,
    };
}
