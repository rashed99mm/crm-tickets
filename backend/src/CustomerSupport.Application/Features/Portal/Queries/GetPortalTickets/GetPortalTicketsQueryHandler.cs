using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Portal.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Portal.Queries.GetPortalTickets;

public class GetPortalTicketsQueryHandler(IRepository<Ticket> tickets)
    : IQueryHandler<GetPortalTicketsQuery, Response<IReadOnlyList<PortalTicketListItemDto>>>
{
    public async Task<Response<IReadOnlyList<PortalTicketListItemDto>>> Handle(
        GetPortalTicketsQuery request, CancellationToken ct)
    {
        var items = await tickets.ListProjectedOrderedAsync(
            t => t.CustomerId == request.CustomerId,
            t => new PortalTicketListItemDto(t.Id, t.Reference, t.Subject, t.Status, t.CreatedAt),
            t => t.CreatedAt,
            descending: true,
            ct);

        return Response<IReadOnlyList<PortalTicketListItemDto>>.Ok(
            items, ApplicationErrors.General.SUCCESS_OPERATION, "OK");
    }
}