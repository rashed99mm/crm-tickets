using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Commands.AddTicketLink;

/// <summary>
/// US-925. The cross-ticket guards live here — the entity cannot see other tickets. An unknown
/// target reference is a field-keyed 400 (the collection exists, the payload is wrong — the AC-31
/// reasoning); an existing row or a direct duplicate cycle is a 409 (well-formed, state is wrong).
/// </summary>
public class AddTicketLinkCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketLink> links,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<AddTicketLinkCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(AddTicketLinkCommand request, CancellationToken ct)
    {
        var source = await tickets.GetByIdAsync(request.TicketId, ct);
        if (source is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var target = await tickets.FirstOrDefaultAsync(
            t => t.Reference == request.TargetReference.Trim(), ct);
        if (target is null)
        {
            return messages.Validation<Guid>(ApplicationErrors.General.VALIDATION_ERROR,
                [new FieldError("TargetReference", SystemCodeMap.Resolve(ApplicationErrors.Ticket.LINK_TARGET_NOT_FOUND), ApplicationErrors.Ticket.LINK_TARGET_NOT_FOUND)]);
        }

        if (target.Id == source.Id)
        {
            return messages.Validation<Guid>(ApplicationErrors.General.VALIDATION_ERROR,
                [new FieldError("TargetReference", SystemCodeMap.Resolve(ApplicationErrors.Ticket.LINK_SELF), ApplicationErrors.Ticket.LINK_SELF)]);
        }

        var linkType = request.LinkType.Trim();

        if (await links.ExistsAsync(l =>
                l.SourceTicketId == source.Id && l.TargetTicketId == target.Id && l.LinkType == linkType, ct))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.LINK_EXISTS, MessageType.Conflict);
        }

        // AC-925.2 / spec A7: only the direct two-ticket cycle is refused; longer chains are legal.
        if (linkType == "DuplicateOf" && await links.ExistsAsync(l =>
                l.SourceTicketId == target.Id && l.TargetTicketId == source.Id && l.LinkType == "DuplicateOf", ct))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.LINK_CYCLE, MessageType.Conflict);
        }

        var link = TicketLink.Create(source.Id, target.Id, linkType, userContext.UserId);
        await links.AddAsync(link, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(link.Id, ApplicationErrors.Ticket.LINK_CREATED);
    }
}
