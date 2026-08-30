using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Commands.TakeEscalation;

public class TakeEscalationCommandHandler(
    IRepository<Ticket> tickets,
    IIdentityUserService identityUsers,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IUserContext userContext,
    IMessageFactory messages)
    : ICommandHandler<TakeEscalationCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(TakeEscalationCommand request, CancellationToken ct)
    {
        var ticket = await tickets.GetTrackedAsync(request.TicketId, ct);

        if (ticket is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var target = await identityUsers.FindByIdAsync(request.AssigneeId, ct);
        if (target is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.ASSIGNEE_NOT_FOUND);
        }

        var roles = await identityUsers.GetRolesAsync(target);
        if (!roles.Contains(ApplicationRole.Roles.Agent))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.ASSIGNEE_NOT_AN_AGENT, MessageType.Conflict);
        }

        if (!target.IsActive)
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.ASSIGNEE_DEACTIVATED, MessageType.Conflict);
        }

        try
        {
            ticket.TakeEscalation(request.AssigneeId, userContext.UserId);
        }
        catch (InvalidOperationException)
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.TRANSITION_NOT_ALLOWED, MessageType.Conflict);
        }

        tickets.SetOriginalValue(ticket, nameof(Ticket.RowVersion), Convert.FromBase64String(request.RowVersion));

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsConcurrencyViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.MODIFIED_BY_ANOTHER_USER, MessageType.Conflict);
        }

        return messages.Success(ticket.Id, ApplicationErrors.Ticket.ESCALATION_OWNER_SET);
    }
}
