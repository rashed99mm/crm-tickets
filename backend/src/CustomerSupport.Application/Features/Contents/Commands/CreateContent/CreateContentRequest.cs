namespace CustomerSupport.Application.Features.Contents.Commands.CreateContent;

public record CreateContentRequest(
    string Title,
    string Body,
    string? Summary,
    string ContentType,
    Guid AuthorId,
    string Status,
    string? FeaturedImageUrl,
    string[] Tags,
    string? Category,
    DateTime? ExpiresAt,
    bool IsFeatured
);
