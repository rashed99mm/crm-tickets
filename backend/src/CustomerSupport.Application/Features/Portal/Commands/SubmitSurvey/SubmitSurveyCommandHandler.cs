using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Survey;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Application.Features.Portal.Commands.SubmitSurvey;

/// <summary>Records a customer's satisfaction survey for a resolved ticket they own (US-408/US-409, PJ-11/12).</summary>
public class SubmitSurveyCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<SurveyResponse> surveys,
    IUnitOfWork unitOfWork,
    IMessageFactory messageFactory)
    : ICommandHandler<SubmitSurveyCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(SubmitSurveyCommand request, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(request.TicketId, ct);
        if (ticket is null)
        {
            return messageFactory.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        if (ticket.CustomerId != request.CustomerId)
        {
            return messageFactory.Fail<Guid>(ApplicationErrors.General.FORBIDDEN, MessageType.Forbidden);
        }

        // A8. A survey is only meaningful once the ticket is closed out.
        if (ticket.Status != TicketStatus.Resolved.Value && ticket.Status != TicketStatus.Closed.Value)
        {
            return messageFactory.Fail<Guid>(ApplicationErrors.Survey.TICKET_NOT_RESOLVED, MessageType.BusinessRule);
        }

        if (await surveys.ExistsAsync(s => s.TicketId == ticket.Id, ct))
        {
            return messageFactory.Fail<Guid>(ApplicationErrors.Survey.ALREADY_SUBMITTED, MessageType.Conflict);
        }

        var response = SurveyResponse.Create(ticket.Id, request.Rating, request.Comment);
        await surveys.AddAsync(response, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return messageFactory.Success(response.Id, ApplicationErrors.Survey.SUBMITTED);
    }
}