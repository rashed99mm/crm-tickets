using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Commands.CreateTicket;

/// <summary>Raises a ticket — AC-29, AC-30, AC-31, BASE-11.</summary>
public class CreateTicketCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<Customer> customers,
    IRepository<Category> categories,
    IRepository<SLAPolicy> slaPolicies,
    IBusinessHoursCalculator businessHoursCalculator,
    ITicketReferenceGenerator references,
    IUserContext userContext,
    IIdentityUserService identityUsers,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<CreateTicketCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateTicketCommand request, CancellationToken ct)
    {
        var missing = new List<FieldError>();

        if (!await customers.ExistsAsync(c => c.Id == request.CustomerId, ct))
        {
            missing.Add(new FieldError("CustomerId", SystemCodeMap.Resolve(ApplicationErrors.Ticket.CUSTOMER_NOT_FOUND), ApplicationErrors.Ticket.CUSTOMER_NOT_FOUND));
        }

        if (!await categories.ExistsAsync(c => c.Id == request.CategoryId && c.IsActive, ct))
        {
            missing.Add(new FieldError("CategoryId", SystemCodeMap.Resolve(ApplicationErrors.Ticket.CATEGORY_NOT_FOUND), ApplicationErrors.Ticket.CATEGORY_NOT_FOUND));
        }

        if (missing.Count > 0)
        {
            return messages.Validation<Guid>(ApplicationErrors.General.VALIDATION_ERROR, missing);
        }

        var reference = await references.NextAsync(ct);

        var ticket = Ticket.Create(
            reference,
            request.Subject,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            request.Priority,
            userContext.UserId);

        var actor = await identityUsers.FindByIdAsync(userContext.UserId, ct);
        if (actor is not null)
        {
            ticket.InheritOrganisation(actor.DepartmentId, actor.BranchId, actor.TeamId);
        }

        // PJ-5. A portal (or any channel) submission stamps the ticket's source. Staff pass null and
        // the ticket keeps the platform default — SetSource refuses null, so only opt-in origins set it.
        if (request.Source is { } source)
        {
            ticket.SetSource(source);
        }

        await ApplySlaTargetsAsync(ticket, ct);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(ticket.Id, ApplicationErrors.Ticket.CREATED);
    }

    /// <summary>
    /// FEAT-17, AC-128..AC-130. Picks the most specific active policy matching the ticket's
    /// priority — one scoped to the ticket's category beats an unscoped one (spec A5). Wall-clock
    /// hours only this slice (spec A1); no matching policy leaves both due dates null (AC-129).
    /// </summary>
    private async Task ApplySlaTargetsAsync(Ticket ticket, CancellationToken ct)
    {
        var candidates = await slaPolicies.ListAsync(
            p => p.IsActive && p.Priority == ticket.Priority
                && (p.CategoryId == null || p.CategoryId == ticket.CategoryId)
                && (p.BranchId == null || p.BranchId == ticket.BranchId),
            ct);

        var policy = candidates
            .OrderByDescending(p => (p.CategoryId.HasValue ? 1 : 0) + (p.BranchId.HasValue ? 1 : 0))
            .FirstOrDefault();

        if (policy is null)
        {
            return;
        }

        ticket.SetSlaTargets(
            await businessHoursCalculator.AddBusinessHours(ticket.CreatedAt, policy.ResponseTargetHours, ticket.BranchId, ct),
            await businessHoursCalculator.AddBusinessHours(ticket.CreatedAt, policy.ResolutionTargetHours, ticket.BranchId, ct));
    }
}
