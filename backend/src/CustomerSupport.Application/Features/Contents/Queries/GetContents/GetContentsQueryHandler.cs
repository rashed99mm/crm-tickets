using CustomerSupport.Application.Common;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Contents.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;
using MediatR;

namespace CustomerSupport.Application.Features.Contents.Queries.GetContents;

public class GetContentsQueryHandler(
    IRepository<Content> contentRepository,
    IRepository<ContentCategory> categoryRepository)
    : IQueryHandler<GetContentsQuery, Response<PaginatedList<ContentDto>>>
{
    public async Task<Response<PaginatedList<ContentDto>>> Handle(GetContentsQuery request, CancellationToken ct)
    {
        // AC-182/183 — the search term is diacritic-folded before the query runs; the LIKE
        // predicate itself can't call Fold (it's plain C#, not SQL-translatable), so only the
        // term is normalized. Whether SQL Server's own default collation additionally folds
        // diacritics on the stored side was not verified against the real database this pass —
        // recorded as a gap, not assumed.
        var searchTerm = string.IsNullOrWhiteSpace(request.SearchTerm) ? null : ArabicTextNormalizer.Fold(request.SearchTerm);

        var filter = PredicateBuilder.True<Content>()
            .WhereIf(searchTerm != null,
                c => c.Title.Contains(searchTerm!) || c.Body.Contains(searchTerm!))
            .WhereIf(!string.IsNullOrWhiteSpace(request.Status),
                c => c.Status == request.Status)
            .WhereIf(request.AuthorId.HasValue,
                c => c.AuthorId == request.AuthorId!.Value)
            .WhereIf(request.CategoryId.HasValue,
                c => c.CategoryId == request.CategoryId!.Value);

        var result = await contentRepository.GetPagedAsync<ContentDto>(request, filter, ct);

        // US-503 — AutoMapper fills IsFaq/CategoryId by convention; the category NAME has no
        // navigation to project through, so it is resolved in one round trip for the page.
        var categoryIds = result.Items
            .Where(i => i.CategoryId.HasValue).Select(i => i.CategoryId!.Value).Distinct().ToList();
        if (categoryIds.Count > 0)
        {
            var namesById = (await categoryRepository.ListAsync(c => categoryIds.Contains(c.Id), ct))
                .ToDictionary(c => c.Id, c => c.Name);
            var patched = result.Items
                .Select(i => i.CategoryId.HasValue
                    ? i with { CategoryName = namesById.GetValueOrDefault(i.CategoryId.Value) }
                    : i)
                .ToList();
            result = PaginatedList<ContentDto>.Create(patched, result.TotalCount, result.PageIndex, result.PageSize);
        }

        return Response<PaginatedList<ContentDto>>.Ok(result, SystemCodeMap.Resolve("SUCCESS_OPERATION"), "OK");
    }
}
