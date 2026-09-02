using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Application.Features.Tickets.Commands.ChangeTicketStatus;

public class ChangeTicketStatusCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketLink> links,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IUserContext userContext,
    IMessageFactory messages)
    : ICommandHandler<ChangeTicketStatusCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(ChangeTicketStatusCommand request, CancellationToken ct)
    {
        var ticket = await tickets.GetTrackedAsync(request.TicketId, ct);

        if (ticket is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var isSupervisor = userContext.HasAnyRole(ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Admin);
        if (!isSupervisor && !ticket.IsAssignedTo(userContext.UserId))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.NOT_ASSIGNED_TO_YOU, MessageType.Forbidden);
        }

        var current = TicketStatus.Create(ticket.Status);
        var target = TicketStatus.Create(request.Status);

        if (current == target)
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.ALREADY_IN_STATUS, MessageType.Conflict);
        }

        if (!current.CanTransitionTo(target))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.TRANSITION_NOT_ALLOWED, MessageType.Conflict);
        }

        // AC-922.2. The validator guarantees both fields when the target is Resolved; for any other
        // target stray resolution fields are ignored rather than refused (they change nothing).
        var resolution = request is { ResolutionCode: not null, ResolutionNotes: not null }
            ? new ResolutionDetails(request.ResolutionCode, request.ResolutionNotes)
            : null;

        // AC-925.3 / spec A8: "Duplicate" is a claim about another ticket — it must be backed by a
        // DuplicateOf link. A state check, not a shape check, so it is a 409 here and not in the
        // validator (which cannot see the link table).
        if (resolution?.Code == "Duplicate" && !await links.ExistsAsync(
                l => l.SourceTicketId == ticket.Id && l.LinkType == "DuplicateOf", ct))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.DUPLICATE_REQUIRES_LINK, MessageType.Conflict);
        }

        // AC-505/AC-503: the aggregate's in-transition guards (assignee required for a work state,
        // resolution required to resolve) throw InvalidOperationException. Caught here, the same
        // pattern TakeEscalationCommandHandler uses, so the refusal reaches the client as the
        // existing 409 rather than an unhandled 500 — a pre-existing gap fixed while touching this
        // handler for AC-922/AC-923, not something either feature introduced.
        try
        {
            ticket.ChangeStatus(request.Status, userContext.UserId, resolution);
        }
        catch (InvalidOperationException)
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.TRANSITION_NOT_ALLOWED, MessageType.Conflict);
        }

        return await SaveAsync(ticket, request.RowVersion, ApplicationErrors.Ticket.STATUS_CHANGED, ct);
    }

    private async Task<Response<Guid>> SaveAsync(Ticket ticket, string rowVersion, string successCode, CancellationToken ct)
    {
        tickets.SetOriginalValue(ticket, nameof(Ticket.RowVersion), Convert.FromBase64String(rowVersion));

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsConcurrencyViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.MODIFIED_BY_ANOTHER_USER, MessageType.Conflict);
        }

        return messages.Success(ticket.Id, successCode);
    }
}
