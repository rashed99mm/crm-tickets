using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Portal.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Survey;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Portal.Queries.GetPortalTicketDetail;

public class GetPortalTicketDetailQueryHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketMessage> messages,
    IRepository<SurveyResponse> surveys,
    IMessageFactory messageFactory)
    : IQueryHandler<GetPortalTicketDetailQuery, Response<PortalTicketDetailDto>>
{
    public async Task<Response<PortalTicketDetailDto>> Handle(GetPortalTicketDetailQuery request, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(request.TicketId, ct);
        if (ticket is null)
        {
            return messageFactory.NotFound<PortalTicketDetailDto>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        // PJ-9. A customer may only ever read their own tickets. A not-found would leak existence,
        // so a row owned by someone else is a 403, not a 404.
        if (ticket.CustomerId != request.CustomerId)
        {
            return messageFactory.Fail<PortalTicketDetailDto>(ApplicationErrors.General.FORBIDDEN, MessageType.Forbidden);
        }

        var thread = await messages.ListOrderedAsync(
            m => m.TicketId == ticket.Id,
            m => m.SentAt,
            descending: false,
            ct);

        var surveySubmitted = await surveys.ExistsAsync(s => s.TicketId == ticket.Id, ct);

        var detail = new PortalTicketDetailDto(
            ticket.Id,
            ticket.Reference,
            ticket.Subject,
            ticket.Description,
            ticket.Status,
            ticket.Priority,
            ticket.CreatedAt,
            [.. thread.Select(m => new PortalMessageDto(m.Direction, m.Body, m.SentAt))],
            surveySubmitted);

        return messageFactory.Success(detail, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}