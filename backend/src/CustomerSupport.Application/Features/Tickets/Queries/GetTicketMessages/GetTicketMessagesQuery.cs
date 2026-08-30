using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Tickets.Dtos;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetTicketMessages;

/// <summary>A ticket's message timeline, oldest first — AC-106. Unpaginated, like TicketHistory: a
/// timeline renders in full on one screen (spec A6).</summary>
public record GetTicketMessagesQuery(Guid TicketId) : IQuery<Response<IReadOnlyList<TicketMessageDto>>>;
