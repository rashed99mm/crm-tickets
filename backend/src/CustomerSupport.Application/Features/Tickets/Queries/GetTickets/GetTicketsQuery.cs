using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Tickets.Dtos;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetTickets;

/// <summary>The queue — AC-32, AC-33, AC-34.</summary>
public class GetTicketsQuery : BasePagedQuery, IQuery<Response<PaginatedList<TicketListItemDto>>>
{
    public string? Status { get; init; }
    public string? Priority { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? AssigneeId { get; init; }
    public bool Mine { get; init; }
    public bool Unassigned { get; init; }
}
