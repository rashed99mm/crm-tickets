using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Commands.RemoveTicketLink;

/// <summary>
/// AC-925.4. Removing a link from a ticket already resolved as Duplicate is allowed — the
/// resolution stands; history is not rewritten.
/// </summary>
public class RemoveTicketLinkCommandHandler(
    IRepository<TicketLink> links,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<RemoveTicketLinkCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(RemoveTicketLinkCommand request, CancellationToken ct)
    {
        var link = await links.FirstOrDefaultAsync(
            l => l.Id == request.LinkId
                && (l.SourceTicketId == request.TicketId || l.TargetTicketId == request.TicketId),
            ct);

        if (link is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.LINK_NOT_FOUND);
        }

        links.Remove(link);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(link.Id, ApplicationErrors.Ticket.LINK_REMOVED);
    }
}
