using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Contents.Queries.GetContentVersions;

public class GetContentVersionsQueryHandler(
    IRepository<ContentVersion> contentVersionRepository,
    IMessageFactory messages)
    : IQueryHandler<GetContentVersionsQuery, Response<IReadOnlyList<ContentVersionDto>>>
{
    public async Task<Response<IReadOnlyList<ContentVersionDto>>> Handle(GetContentVersionsQuery request, CancellationToken ct)
    {
        var versions = await contentVersionRepository.ListOrderedAsync(
            v => v.ContentId == request.ContentId,
            v => v.VersionNumber,
            descending: true,
            ct);

        var dtos = versions
            .Select(v => new ContentVersionDto(v.VersionNumber, v.AuthorId, v.ChangeSummary, v.CreatedAt))
            .ToList();

        return messages.Success<IReadOnlyList<ContentVersionDto>>(dtos, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
