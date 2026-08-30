namespace CustomerSupport.Application.Features.Contents.Dtos;

public record ContentDto(
    Guid Id,
    string Title,
    string Body,
    string? Summary,
    string ContentType,
    Guid AuthorId,
    string Status,
    string? FeaturedImageUrl,
    int ViewCount,
    int LikeCount,
    string[] Tags,
    string? Category,
    DateTime? PublishedAt,
    DateTime? ExpiresAt,
    bool IsFeatured,
    DateTime CreatedAt,
    int Version,
    int DislikeCount,
    // FEAT-18 (US-503/504) — taxonomy + FAQ surfaced to both hosts. CategoryName defaults so the
    // hand-written positional mappers stay source-compatible; it is resolved per-handler from
    // ContentCategory (no navigation property exists on Content by design).
    bool IsFaq = false,
    Guid? CategoryId = null,
    string? CategoryName = null
);
