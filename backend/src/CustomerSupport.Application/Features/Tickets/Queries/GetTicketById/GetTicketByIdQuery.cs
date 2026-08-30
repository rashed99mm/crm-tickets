using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Tickets.Dtos;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetTicketById;

public record GetTicketByIdQuery(Guid Id) : IQuery<Response<TicketDetailDto>>;
