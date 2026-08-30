using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Contents.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Contents.Queries.GetContentById;

public class GetContentByIdQueryHandler(
    IRepository<Content> contentRepository,
    IRepository<ContentView> contentViewRepository,
    IRepository<ContentCategory> categoryRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IMessageFactory messages,
    ILogger<GetContentByIdQueryHandler> logger)
    : IQueryHandler<GetContentByIdQuery, Response<ContentDto>>
{
    public async Task<Response<ContentDto>> Handle(GetContentByIdQuery request, CancellationToken ct)
    {
        logger.LogInformation("Retrieving content {ContentId}", request.Id);

        var content = await contentRepository.GetByIdAsync(request.Id, ct);
        if (content == null)
        {
            logger.LogWarning("Content {ContentId} not found", request.Id);
            return messages.NotFound<ContentDto>(ApplicationErrors.Content.NOT_FOUND);
        }

        if (content.IsPublished)
        {
            // AC-185/186. Only published content counts toward the public view metric — a draft
            // viewed via the internal-only read (which reuses this same handler) must not inflate
            // a count nobody outside staff can see yet. IUserContext.UserId returns Guid.Empty for
            // an anonymous caller rather than null, so IsAuthenticated is the real signal.
            var viewerId = userContext.IsAuthenticated ? userContext.UserId : (Guid?)null;
            content.IncrementViewCount();
            contentRepository.Update(content);
            await contentViewRepository.AddAsync(ContentView.Create(content.Id, viewerId), ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        var dto = MapToDto(content);
        if (content.CategoryId is not null)
        {
            // US-503 — the taxonomy name travels with the article; the free-string Category is
            // deprecated and usually empty for articles created through the new path.
            var category = await categoryRepository.FirstOrDefaultAsync(c => c.Id == content.CategoryId, ct);
            dto = dto with { CategoryName = category?.Name };
        }

        return messages.Success(dto, ApplicationErrors.General.SUCCESS_OPERATION);
    }

    private static ContentDto MapToDto(Content content) => new(
        content.Id,
        content.Title,
        content.Body,
        content.Summary,
        content.ContentType,
        content.AuthorId,
        content.Status,
        content.FeaturedImageUrl,
        content.ViewCount,
        content.LikeCount,
        content.Tags,
        content.Category,
        content.PublishedAt,
        content.ExpiresAt,
        content.IsFeatured,
        content.CreatedAt,
        content.Version,
        content.DislikeCount,
        content.IsFaq,
        content.CategoryId
    );
}
