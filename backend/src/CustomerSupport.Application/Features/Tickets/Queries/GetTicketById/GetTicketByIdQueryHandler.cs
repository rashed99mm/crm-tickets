using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Tickets.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetTicketById;

public class GetTicketByIdQueryHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketHistory> history,
    IRepository<Customer> customers,
    IRepository<Category> categories,
    IIdentityUserService identityUsers,
    IUserContext userContext,
    IMessageFactory messages)
    : IQueryHandler<GetTicketByIdQuery, Response<TicketDetailDto>>
{
    public async Task<Response<TicketDetailDto>> Handle(GetTicketByIdQuery request, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(request.Id, ct);
        if (ticket is null)
        {
            return messages.NotFound<TicketDetailDto>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var requestingUser = await identityUsers.FindByIdAsync(userContext.UserId, ct);
        if (requestingUser?.BranchId is { } branchId && ticket.BranchId != branchId)
        {
            return messages.NotFound<TicketDetailDto>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct);
        var category = await categories.GetByIdAsync(ticket.CategoryId, ct);

        var entries = await history.ListOrderedAsync(
            h => h.TicketId == ticket.Id,
            h => h.OccurredAt,
            descending: true,
            ct);

        var actorNames = new Dictionary<Guid, string>();
        foreach (var actorId in entries.Select(e => e.ActorId).Distinct())
        {
            var actor = await identityUsers.FindByIdAsync(actorId, ct);
            actorNames[actorId] = actor?.FullName ?? string.Empty;
        }

        string? assigneeName = null;
        if (ticket.AssigneeId.HasValue)
        {
            var assignee = await identityUsers.FindByIdAsync(ticket.AssigneeId.Value, ct);
            assigneeName = assignee?.FullName;
        }

        string? escalationAssigneeName = null;
        if (ticket.EscalationAssigneeId.HasValue)
        {
            var escalationAssignee = await identityUsers.FindByIdAsync(ticket.EscalationAssigneeId.Value, ct);
            escalationAssigneeName = escalationAssignee?.FullName;
        }

        var detail = new TicketDetailDto(
            ticket.Id,
            ticket.Reference,
            ticket.Subject,
            ticket.Description,
            ticket.Status,
            ticket.Priority,
            ticket.AssigneeId,
            assigneeName,
            ticket.CreatedAt,
            Convert.ToBase64String(ticket.RowVersion ?? []),
            new CustomerSummaryDto(
                customer?.Id ?? ticket.CustomerId,
                customer?.Name ?? string.Empty,
                customer?.Email ?? string.Empty,
                customer?.Phone),
            category?.Name ?? string.Empty,
            [.. entries.Select(e => new TicketHistoryDto(
                e.Id,
                e.ChangeType,
                e.FromValue,
                e.ToValue,
                e.ActorId,
                actorNames.GetValueOrDefault(e.ActorId, string.Empty),
                e.OccurredAt))],
            ticket.ResponseDueAt,
            ticket.ResolutionDueAt,
            ticket.EscalationState,
            ticket.FirstResponseAt,
            ticket.LastResponseAt,
            ticket.ResolvedAt,
            ticket.ClosedAt,
            ticket.EscalationAssigneeId,
            escalationAssigneeName);

        return messages.Success(detail, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
