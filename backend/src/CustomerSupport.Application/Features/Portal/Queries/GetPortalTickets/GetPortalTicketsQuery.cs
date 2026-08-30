using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Portal.Dtos;

namespace CustomerSupport.Application.Features.Portal.Queries.GetPortalTickets;

/// <summary>Lists the calling customer's own tickets, newest first (US-405, PJ-8).</summary>
public record GetPortalTicketsQuery(Guid CustomerId)
    : IQuery<Response<IReadOnlyList<PortalTicketListItemDto>>>;