using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.ContentCategories.Queries.GetContentCategoryTree;

/// <summary>AC-174 — categories nested under their parents, not a flat list.</summary>
public record GetContentCategoryTreeQuery : IQuery<Response<IReadOnlyList<ContentCategoryNodeDto>>>;

public record ContentCategoryNodeDto(
    Guid Id,
    string Name,
    Guid? ParentId,
    IReadOnlyList<ContentCategoryNodeDto> Children);
