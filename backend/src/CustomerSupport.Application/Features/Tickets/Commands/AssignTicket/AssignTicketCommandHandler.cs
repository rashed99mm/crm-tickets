using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Commands.AssignTicket;

public class AssignTicketCommandHandler(
    IRepository<Ticket> tickets,
    IIdentityUserService identityUsers,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IUserContext userContext,
    IMessageFactory messages)
    : ICommandHandler<AssignTicketCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(AssignTicketCommand request, CancellationToken ct)
    {
        var ticket = await tickets.GetTrackedAsync(request.TicketId, ct);

        if (ticket is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var target = await identityUsers.FindByIdAsync(request.AssigneeId, ct);
        if (target is null)
        {
            return FieldFailure(ApplicationErrors.Ticket.ASSIGNEE_NOT_FOUND);
        }

        var roles = await identityUsers.GetRolesAsync(target);
        if (!roles.Contains(ApplicationRole.Roles.Agent))
        {
            return FieldFailure(ApplicationErrors.Ticket.ASSIGNEE_NOT_AN_AGENT);
        }

        if (!target.IsActive)
        {
            return FieldFailure(ApplicationErrors.Ticket.ASSIGNEE_DEACTIVATED);
        }

        ticket.InheritOrganisation(target.DepartmentId, target.BranchId, target.TeamId);

        var isSupervisor = userContext.HasAnyRole(ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Admin);
        if (!isSupervisor && request.AssigneeId != userContext.UserId)
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.ASSIGNMENT_REFUSED, MessageType.Forbidden);
        }

        ticket.AssignTo(request.AssigneeId, userContext.UserId);

        tickets.SetOriginalValue(ticket, nameof(Ticket.RowVersion), Convert.FromBase64String(request.RowVersion));

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsConcurrencyViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.MODIFIED_BY_ANOTHER_USER, MessageType.Conflict);
        }

        return messages.Success(ticket.Id, ApplicationErrors.Ticket.ASSIGNED);
    }

    private Response<Guid> FieldFailure(string code)
    {
        var fieldErrors = new List<FieldError>
        {
            new("AssigneeId", SystemCodeMap.Resolve(code), code)
        };

        return messages.Validation<Guid>(ApplicationErrors.General.VALIDATION_ERROR, fieldErrors);
    }
}
