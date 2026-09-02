using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Application.Features.Tickets.Commands.AddTicketTag;

/// <summary>
/// US-924. Orchestrated over the tag repository (the `TicketNote` pattern) because the ticket
/// aggregate is loaded without its child collections; the duplicate/limit checks therefore run
/// against the committed rows, and the history row is appended explicitly (AC-924.3).
/// </summary>
public class AddTicketTagCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketTag> ticketTags,
    IRepository<TicketHistory> history,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<AddTicketTagCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(AddTicketTagCommand request, CancellationToken ct)
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
            return messages.Validation<Guid>(ApplicationErrors.General.VALIDATION_ERROR,
                [new FieldError("Value", SystemCodeMap.Resolve(ApplicationErrors.Validation.TICKET_TAG_INVALID), ApplicationErrors.Validation.TICKET_TAG_INVALID)]);
        }

        var existing = await ticketTags.ListAsync(t => t.TicketId == request.TicketId, ct);

        if (existing.Any(t => t.Value == normalized))
        {
            return messages.Validation<Guid>(ApplicationErrors.General.VALIDATION_ERROR,
                [new FieldError("Value", SystemCodeMap.Resolve(ApplicationErrors.Validation.TICKET_TAG_DUPLICATE), ApplicationErrors.Validation.TICKET_TAG_DUPLICATE)]);
        }

        if (existing.Count >= TagValue.MaxPerTicket)
        {
            return messages.Validation<Guid>(ApplicationErrors.General.VALIDATION_ERROR,
                [new FieldError("Value", SystemCodeMap.Resolve(ApplicationErrors.Validation.TICKET_TAG_LIMIT), ApplicationErrors.Validation.TICKET_TAG_LIMIT)]);
        }

        var tag = TicketTag.Create(request.TicketId, normalized, userContext.UserId);
        await ticketTags.AddAsync(tag, ct);
        await history.AddAsync(
            TicketHistory.Record(request.TicketId, userContext.UserId, TicketChangeType.TagAdded, null, normalized), ct);

        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(tag.Id, ApplicationErrors.Ticket.TAG_ADDED);
    }
}
