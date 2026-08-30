using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Contents.Queries.GetLinkedContent;

/// <summary>AC-181 — every article linked to a ticket, with enough metadata to render.</summary>
public record GetLinkedContentQuery(Guid TicketId) : IQuery<Response<IReadOnlyList<LinkedContentDto>>>;

public record LinkedContentDto(Guid ContentId, string Title, string Status, Guid LinkedByAgentId, DateTime LinkedAt);
