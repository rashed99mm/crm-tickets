using CustomerSupport.Domain.Entities;

namespace CustomerSupport.Domain.Entities.Content;

/// <summary>FEAT-11, AC-187/188 — one user's current vote on an article. Not append-only: a
/// changed vote updates this row in place rather than appending a new one, matching AC-188's
/// "never a second row for the same (ContentId, UserId)."</summary>
public class ContentVote : BaseEntity
{
    public Guid ContentId { get; private set; }
    public Guid UserId { get; private set; }
    public bool IsHelpful { get; private set; }
    public DateTime VotedAt { get; private set; }

    public static ContentVote Create(Guid contentId, Guid userId, bool isHelpful) => new()
    {
        Id = Guid.NewGuid(),
        ContentId = contentId,
        UserId = userId,
        IsHelpful = isHelpful,
        VotedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
    };

    public void ChangeTo(bool isHelpful)
    {
        IsHelpful = isHelpful;
        VotedAt = DateTime.UtcNow;
        MarkUpdated();
    }
}
