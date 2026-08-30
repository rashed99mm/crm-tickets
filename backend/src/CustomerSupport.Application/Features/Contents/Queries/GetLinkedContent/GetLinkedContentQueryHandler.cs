using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Contents.Queries.GetLinkedContent;

public class GetLinkedContentQueryHandler(
    IRepository<ContentTicketLink> linkRepository,
    IRepository<Content> contentRepository,
    IMessageFactory messages)
    : IQueryHandler<GetLinkedContentQuery, Response<IReadOnlyList<LinkedContentDto>>>
{
    public async Task<Response<IReadOnlyList<LinkedContentDto>>> Handle(GetLinkedContentQuery request, CancellationToken ct)
    {
        var links = await linkRepository.ListAsync(l => l.TicketId == request.TicketId, ct);
        if (links.Count == 0)
        {
            return messages.Success<IReadOnlyList<LinkedContentDto>>([], ApplicationErrors.General.SUCCESS_OPERATION);
        }

        var contentIds = links.Select(l => l.ContentId).Distinct().ToList();
        var contents = await contentRepository.ListAsync(c => contentIds.Contains(c.Id), ct);
        var contentMap = contents.ToDictionary(c => c.Id);

        var dtos = links
            .Where(l => contentMap.ContainsKey(l.ContentId))
            .Select(l =>
            {
                var content = contentMap[l.ContentId];
                return new LinkedContentDto(content.Id, content.Title, content.Status, l.LinkedByAgentId, l.LinkedAt);
            })
            .ToList();

        return messages.Success<IReadOnlyList<LinkedContentDto>>(dtos, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
