using CustomerSupport.Application.Contracts;

using MediatR;

namespace CustomerSupport.Application.Features.Contents.Commands.UpdateContent;

public record UpdateContentCommand(
    Guid Id,
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
) : ICommand<Response<Guid>>;
