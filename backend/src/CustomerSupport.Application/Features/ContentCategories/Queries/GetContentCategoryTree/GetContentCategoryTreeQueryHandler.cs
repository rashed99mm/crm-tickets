using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.ContentCategories.Queries.GetContentCategoryTree;

/// <summary>Builds the tree in memory — the category count here is small (dozens, not
/// thousands), the same trade-off this codebase already makes for its other small-dataset
/// in-memory joins (e.g. the ticket queue's customer/category name resolution).</summary>
public class GetContentCategoryTreeQueryHandler(
    IRepository<ContentCategory> categoryRepository,
    IMessageFactory messages)
    : IQueryHandler<GetContentCategoryTreeQuery, Response<IReadOnlyList<ContentCategoryNodeDto>>>
{
    public async Task<Response<IReadOnlyList<ContentCategoryNodeDto>>> Handle(GetContentCategoryTreeQuery request, CancellationToken ct)
    {
        var categories = await categoryRepository.ListAsync(c => c.IsActive, ct);

        // ToLookup, not ToDictionary: root categories group under a null ParentId key, and
        // System.Collections.Generic.Dictionary throws ArgumentNullException on a null key even
        // when TKey is a nullable value type. ILookup permits it and returns an empty sequence
        // for any key (present or not), which also removes the need for TryGetValue below.
        var byParent = categories.ToLookup(c => c.ParentId);

        IReadOnlyList<ContentCategoryNodeDto> BuildLevel(Guid? parentId) =>
            byParent[parentId]
                .Select(c => new ContentCategoryNodeDto(c.Id, c.Name, c.ParentId, BuildLevel(c.Id)))
                .ToList();

        return messages.Success(BuildLevel(null), ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
