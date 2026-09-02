using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Commands.ReclassifyTicket;

/// <summary>
/// US-923 / AC-923.2. Same per-record rule as a status change: an agent may reclassify only a
/// ticket assigned to them; a supervisor/admin may reclassify any — decidable only with the ticket
/// loaded.
/// </summary>
public class ReclassifyTicketCommandHandler(
    IRepository<Ticket> tickets,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IUserContext userContext,
    IMessageFactory messages)
    : ICommandHandler<ReclassifyTicketCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(ReclassifyTicketCommand request, CancellationToken ct)
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

        ticket.Reclassify(request.Impact, request.Urgency, userContext.UserId);

        tickets.SetOriginalValue(ticket, nameof(Ticket.RowVersion), Convert.FromBase64String(request.RowVersion));

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsConcurrencyViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.MODIFIED_BY_ANOTHER_USER, MessageType.Conflict);
        }

        return messages.Success(ticket.Id, ApplicationErrors.Ticket.RECLASSIFIED);
    }
}
