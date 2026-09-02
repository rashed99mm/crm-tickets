using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Application.Features.Tickets.Commands.RemoveTicketTag;

public class RemoveTicketTagCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketTag> ticketTags,
    IRepository<TicketHistory> history,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<RemoveTicketTagCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(RemoveTicketTagCommand request, CancellationToken ct)
    {
        if (!await tickets.ExistsAsync(t => t.Id == request.TicketId, ct))
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        string normalized;
        try
        {
            normalized = TagValue.Normalize(request.Value);
        }
        catch (ArgumentException)
        {
            // A value that cannot be a tag cannot be on the ticket — same answer as absent.
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.TAG_NOT_FOUND);
        }

        var tag = await ticketTags.FirstOrDefaultAsync(
            t => t.TicketId == request.TicketId && t.Value == normalized, ct);

        if (tag is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.TAG_NOT_FOUND);
        }

        ticketTags.Remove(tag);
        await history.AddAsync(
            TicketHistory.Record(request.TicketId, userContext.UserId, TicketChangeType.TagRemoved, normalized, null), ct);

        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(tag.Id, ApplicationErrors.Ticket.TAG_REMOVED);
    }
}
