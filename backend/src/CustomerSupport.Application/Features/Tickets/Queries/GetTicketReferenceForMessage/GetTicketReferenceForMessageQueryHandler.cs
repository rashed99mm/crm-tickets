using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetTicketReferenceForMessage;

public class GetTicketReferenceForMessageQueryHandler(
    IRepository<TicketMessage> messages,
    IRepository<Ticket> tickets,
    IMessageFactory messageFactory)
    : IQueryHandler<GetTicketReferenceForMessageQuery, Response<string>>
{
    public async Task<Response<string>> Handle(GetTicketReferenceForMessageQuery request, CancellationToken ct)
    {
        var message = await messages.FirstOrDefaultAsync(m => m.Id == request.MessageId, ct);
        if (message is null)
        {
            // Ticket.NOT_FOUND for both branches: there is no MESSAGE_NOT_FOUND code (verified
            // 2026-09-02), and adding one would need a bilingual Resources.yaml entry or
            // ContractHardeningTests.EveryErrorCode_HasABilingualMessage fails.
            return messageFactory.NotFound<string>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var ticket = await tickets.FirstOrDefaultAsync(t => t.Id == message.TicketId, ct);
        if (ticket is null)
        {
            return messageFactory.NotFound<string>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        return messageFactory.Success(ticket.Reference, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
