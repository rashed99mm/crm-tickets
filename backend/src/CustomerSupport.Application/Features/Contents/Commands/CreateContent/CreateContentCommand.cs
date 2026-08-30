using CustomerSupport.Application.Contracts;

using MediatR;

namespace CustomerSupport.Application.Features.Contents.Commands.CreateContent;

public record CreateContentCommand(
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
) : ICommand<Response<Guid>>;
