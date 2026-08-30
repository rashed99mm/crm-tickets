using System.Linq.Expressions;
using CustomerSupport.Application.Common;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Contents.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Contents.Queries.GetFaqContents;

public class GetFaqContentsQueryHandler(
    IRepository<Content> contentRepository,
    IRepository<ContentCategory> categoryRepository,
    IMessageFactory messages)
    : IQueryHandler<GetFaqContentsQuery, Response<PaginatedList<ContentDto>>>
{
    public async Task<Response<PaginatedList<ContentDto>>> Handle(GetFaqContentsQuery request, CancellationToken ct)
    {
        // The Published filter is defensive: IsFaq can only be *set* on a published article
        // (Content.MarkAsFaq's guard), but a later status change via the generic UpdateStatus
        // path could in principle leave IsFaq true on a non-published row — this keeps the public
        // endpoint honest either way.
        // Search term is diacritic-folded (same as the main articles list) so Arabic searches
        // hit their un-diacriticised rows. Skip/Take give the public bento and the "browse all"
        // experience a single endpoint.
        var searchTerm = string.IsNullOrWhiteSpace(request.SearchTerm)
            ? null
            : ArabicTextNormalizer.Fold(request.SearchTerm);
        var take = request.Take > 0 ? request.Take : 3;
        var skip = request.Skip >= 0 ? request.Skip : 0;

        var baseFilter = PredicateBuilder.True<Content>()
            .And(c => c.IsFaq && c.Status == "Published");

        var filter = string.IsNullOrEmpty(searchTerm)
            ? baseFilter
            : baseFilter.And(c => c.Title.Contains(searchTerm!) || c.Body.Contains(searchTerm!));

        var total = await contentRepository.CountAsync(filter, ct);

        var page = await contentRepository.ListOrderedAsync(
            filter,
            c => c.ViewCount,
            descending: true,
            ct);

        var paged = page.Skip(skip).Take(take).ToList();

        // US-503 — resolve taxonomy names in one round trip for the whole page.
        var categoryIds = paged.Where(f => f.CategoryId.HasValue).Select(f => f.CategoryId!.Value).Distinct().ToList();
        var namesById = categoryIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await categoryRepository.ListAsync(c => categoryIds.Contains(c.Id), ct))
                .ToDictionary(c => c.Id, c => c.Name);

        var dtos = paged
            .Select(f => ToDto(f) with
            {
                CategoryName = f.CategoryId.HasValue
                    ? namesById.GetValueOrDefault(f.CategoryId.Value)
                    : null,
            })
            .ToList();

        var pageIndex = take == 0 ? 1 : (skip / take) + 1;
        var result = PaginatedList<ContentDto>.Create(dtos, total, pageIndex, take);

        return messages.Success(result, ApplicationErrors.General.SUCCESS_OPERATION);
    }

    private static ContentDto ToDto(Content content) => new(
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
