using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Tickets.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetTicketMessages;

public class GetTicketMessagesQueryHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketMessage> messages,
    IIdentityUserService identityUsers,
    IMessageFactory messageFactory)
    : IQueryHandler<GetTicketMessagesQuery, Response<IReadOnlyList<TicketMessageDto>>>
{
    public async Task<Response<IReadOnlyList<TicketMessageDto>>> Handle(GetTicketMessagesQuery request, CancellationToken ct)
    {
        if (!await tickets.ExistsAsync(t => t.Id == request.TicketId, ct))
        {
            return messageFactory.NotFound<IReadOnlyList<TicketMessageDto>>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var rows = await messages.ListOrderedAsync(
            m => m.TicketId == request.TicketId,
            m => m.SentAt,
            descending: false,
            ct);

        var senderNames = new Dictionary<Guid, string>();
        foreach (var senderId in rows.Select(m => m.SenderId).Distinct())
        {
            var sender = await identityUsers.FindByIdAsync(senderId, ct);
            senderNames[senderId] = sender?.FullName ?? string.Empty;
        }

        IReadOnlyList<TicketMessageDto> items = rows.Select(m => new TicketMessageDto(
            m.Id, m.Direction, m.Channel, m.Subject, m.Body,
            m.SenderId, senderNames.GetValueOrDefault(m.SenderId, string.Empty), m.SentAt)).ToList();

        return messageFactory.Success(items, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
