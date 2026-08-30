using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Contents.Queries.GetContentVersions;

/// <summary>AC-170 — an article's version history, newest first.</summary>
public record GetContentVersionsQuery(Guid ContentId) : IQuery<Response<IReadOnlyList<ContentVersionDto>>>;

public record ContentVersionDto(int VersionNumber, Guid AuthorId, string ChangeSummary, DateTime CreatedAt);
