namespace CustomerSupport.Application.Features.Contents.Commands.UpdateContent;

public record UpdateContentRequest(
    string? Title,
    string? Body,
    string? Summary,
    string? Status,
    string? FeaturedImageUrl,
    string[]? Tags,
    string? Category,
    DateTime? PublishedAt,
    DateTime? ExpiresAt,
    bool? IsFeatured
);
