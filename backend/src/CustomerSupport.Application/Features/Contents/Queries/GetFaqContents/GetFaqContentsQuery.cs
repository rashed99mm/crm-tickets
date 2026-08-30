using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Contents.Dtos;
using CustomerSupport.Domain;

namespace CustomerSupport.Application.Features.Contents.Queries.GetFaqContents;

/// <summary>
/// AC-177 — FAQ articles only, distinct from the full article list.
/// Supports search by title/body and pagination via <see cref="Skip"/>/<see cref="Take"/>.
/// </summary>
public record GetFaqContentsQuery(
    string? SearchTerm = null,
    int Skip = 0,
    int Take = 3) : IQuery<Response<PaginatedList<ContentDto>>>;
