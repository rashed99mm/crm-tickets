using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Portal.Dtos;

namespace CustomerSupport.Application.Features.Portal.Queries.GetPortalTicketDetail;

/// <summary>Fetches one of the calling customer's tickets, enforcing ownership (US-406, PJ-9).</summary>
public record GetPortalTicketDetailQuery(Guid TicketId, Guid CustomerId)
    : IQuery<Response<PortalTicketDetailDto>>;