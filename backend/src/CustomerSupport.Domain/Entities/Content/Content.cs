using CustomerSupport.Domain.Entities;
using CustomerSupport.Domain.Events.Content;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Domain.Entities.Content;

public class Content : AggregateRoot
{
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? Summary { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public Guid AuthorId { get; private set; }
    public string Status { get; private set; } = "Draft";
    public string? FeaturedImageUrl { get; private set; }
    public int ViewCount { get; private set; }
    public int LikeCount { get; private set; }
    public string[] Tags { get; private set; } = Array.Empty<string>();
    public string? Category { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public bool IsFeatured { get; private set; }

    /// <summary>FEAT-11, AC-168/169. Every article starts at 1 and bumps on every saved change.</summary>
    public int Version { get; private set; } = 1;

    /// <summary>FEAT-11, AC-171/172. Replaces the free-text <see cref="Category"/> field above
    /// (kept, unwritten, for the rollout window — spec A2). Null until an author assigns one.</summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>FEAT-11, AC-175/176/177.</summary>
    public bool IsFaq { get; private set; }

    /// <summary>FEAT-11, AC-187/188. Mirrors <see cref="LikeCount"/>'s existing shape.</summary>
    public int DislikeCount { get; private set; }

    public static Content Create(string title, string body, string contentType, Guid authorId, string? summary = null, string? category = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));
        if (title.Length > 200)
            throw new ArgumentException("Title must not exceed 200 characters", nameof(title));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required", nameof(body));
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type is required", nameof(contentType));

        return new Content
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Body = body,
            ContentType = contentType,
            AuthorId = authorId,
            Summary = summary?.Trim(),
            Category = category?.Trim(),
            Status = "Draft",
            Tags = Array.Empty<string>(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Publish()
    {
        var currentStatus = ContentStatus.Create(Status);
        var targetStatus = ContentStatus.Published;

        if (!currentStatus.CanTransitionTo(targetStatus))
        {
            throw new InvalidOperationException($"Cannot publish content with status '{Status}'.");
        }

        Status = targetStatus.Value;
        PublishedAt = DateTime.UtcNow;
        MarkUpdated();

        AddDomainEvent(new ContentPublishedEvent(Id, Title, AuthorId, ContentType));
    }

    public void Archive()
    {
        var currentStatus = ContentStatus.Create(Status);
        var targetStatus = ContentStatus.Archived;

        if (!currentStatus.CanTransitionTo(targetStatus))
        {
            throw new InvalidOperationException($"Cannot archive content with status '{Status}'.");
        }

        Status = targetStatus.Value;
        MarkUpdated();

        AddDomainEvent(new ContentArchivedEvent(Id, Title));
    }

    public void IncrementViewCount()
    {
        ViewCount++;
        MarkUpdated();
    }

    public void IncrementLikeCount()
    {
        LikeCount++;
        MarkUpdated();
    }

    public void DecrementLikeCount()
    {
        if (LikeCount > 0)
        {
            LikeCount--;
            MarkUpdated();
        }
    }

    public void IncrementDislikeCount()
    {
        DislikeCount++;
        MarkUpdated();
    }

    public void DecrementDislikeCount()
    {
        if (DislikeCount > 0)
        {
            DislikeCount--;
            MarkUpdated();
        }
    }

    public void UpdateTags(string[] tags)
    {
        if (tags == null || tags.Length == 0)
        {
            Tags = Array.Empty<string>();
        }
        else
        {
            if (tags.Length > 20)
                throw new ArgumentException("Cannot have more than 20 tags", nameof(tags));

            if (tags.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > 50))
                throw new ArgumentException("Each tag must be between 1 and 50 characters", nameof(tags));

            Tags = tags.Select(t => t.Trim().ToLowerInvariant()).Distinct().ToArray();
        }

        MarkUpdated();
    }

    public void SetFeatured()
    {
        if (!IsFeatured)
        {
            IsFeatured = true;
            MarkUpdated();
        }
    }

    public void UnsetFeatured()
    {
        if (IsFeatured)
        {
            IsFeatured = false;
            MarkUpdated();
        }
    }

    public void UpdateContent(string? title, string? body, string? summary, string? category)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            if (title.Length > 200)
                throw new ArgumentException("Title must not exceed 200 characters", nameof(title));
            Title = title.Trim();
        }

        if (body != null)
        {
            Body = body;
        }

        if (summary != null)
        {
            Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        }

        if (category != null)
        {
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        }

        MarkUpdated();
    }

    public void UpdateFeaturedImage(string? featuredImageUrl)
    {
        FeaturedImageUrl = string.IsNullOrWhiteSpace(featuredImageUrl) ? null : featuredImageUrl.Trim();
        MarkUpdated();
    }

    public void UpdateExpiresAt(DateTime? expiresAt)
    {
        ExpiresAt = expiresAt;
        MarkUpdated();
    }

    public void UpdateIsFeatured(bool isFeatured)
    {
        IsFeatured = isFeatured;
        MarkUpdated();
    }

    public void UpdateStatus(string status)
    {
        var currentStatus = ContentStatus.Create(Status);
        var targetStatus = ContentStatus.Create(status);

        if (!currentStatus.CanTransitionTo(targetStatus))
        {
            throw new InvalidOperationException($"Cannot transition from '{Status}' to '{status}'.");
        }

        Status = targetStatus.Value;
        if (targetStatus.IsPublished && !PublishedAt.HasValue)
        {
            PublishedAt = DateTime.UtcNow;
        }
        MarkUpdated();

        if (targetStatus.IsPublished)
        {
            AddDomainEvent(new ContentPublishedEvent(Id, Title, AuthorId, ContentType));
        }
        else if (targetStatus.IsArchived)
        {
            AddDomainEvent(new ContentArchivedEvent(Id, Title));
        }
    }

    public bool IsPublished => Status == "Published";
    public bool IsDraft => Status == "Draft";
    public bool IsArchived => Status == "Archived";

    /// <summary>AC-172. Any status may be recategorized — no transition guard needed.</summary>
    public void AssignCategory(Guid? categoryId)
    {
        CategoryId = categoryId;
        MarkUpdated();
    }

    /// <summary>AC-175/176 — only a Published article may become a FAQ.</summary>
    public void MarkAsFaq()
    {
        if (!IsPublished)
        {
            throw new InvalidOperationException("Only published content may be marked as FAQ.");
        }

        IsFaq = true;
        MarkUpdated();
    }

    /// <summary>AC-175 — the inverse. No status guard: an already-FAQ'd article that later leaves
    /// Published should still be unmarkable.</summary>
    public void UnmarkFaq()
    {
        IsFaq = false;
        MarkUpdated();
    }

    /// <summary>Snapshots the article's current title/body under a new version number, for the
    /// handler to persist as a ContentVersion row in the same SaveChangesAsync. Called by every
    /// mutating command (AC-168) — including Publish/Archive, since a status transition is itself
    /// a change worth recording (AC-165/166's "recorded in the version history").</summary>
    public ContentVersionSnapshot RecordChange(string changeSummary)
    {
        Version++;
        MarkUpdated();
        return new ContentVersionSnapshot(Version, Title, Body, changeSummary);
    }
}

/// <summary>A plain DTO, not an entity — what <see cref="Content.RecordChange"/> hands back to the
/// handler so it can persist a <see cref="ContentVersion"/> row.</summary>
public readonly record struct ContentVersionSnapshot(int VersionNumber, string Title, string Body, string ChangeSummary);
