using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Tickets.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetTickets;

public class GetTicketsQueryHandler(
    IRepository<Ticket> tickets,
    IRepository<Customer> customers,
    IRepository<Category> categories,
    IIdentityUserService identityUsers,
    IUserContext userContext)
    : IQueryHandler<GetTicketsQuery, Response<PaginatedList<TicketListItemDto>>>
{
    public async Task<Response<PaginatedList<TicketListItemDto>>> Handle(GetTicketsQuery request, CancellationToken ct)
    {
        var assigneeId = request.Mine ? userContext.UserId : request.AssigneeId;
        var actor = await identityUsers.FindByIdAsync(userContext.UserId, ct);
        var branchId = actor?.BranchId;

        var filter = PredicateBuilder.True<Ticket>()
            .WhereIf(!string.IsNullOrWhiteSpace(request.Status), t => t.Status == request.Status!)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Priority), t => t.Priority == request.Priority!)
            .WhereIf(request.CustomerId.HasValue, t => t.CustomerId == request.CustomerId!.Value)
            .WhereIf(request.Unassigned && !request.Mine, t => t.AssigneeId == null)
            .WhereIf(assigneeId.HasValue, t => t.AssigneeId == assigneeId!.Value)
            .WhereIf(branchId.HasValue, t => t.BranchId == branchId!.Value);

        var pageIndex = Math.Max(request.PageIndex, 1);
        var pageSize = Math.Max(request.PageSize, 1);

        var total = await tickets.CountAsync(filter, ct);

        var ticketItems = await tickets.ListProjectedOrderedAsync(
            filter,
            t => new { t.Id, t.Reference, t.Subject, t.Status, t.Priority, t.CustomerId, t.CategoryId, t.AssigneeId, t.CreatedAt, t.EscalationState, t.FirstResponseAt, t.LastResponseAt, t.ResolvedAt, t.ClosedAt, t.EscalationAssigneeId },
            t => t.CreatedAt,
            descending: true,
            ct);

        var pagedTickets = ticketItems
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var customerIds = pagedTickets.Select(t => t.CustomerId).Distinct().ToList();
        var categoryIds = pagedTickets.Select(t => t.CategoryId).Distinct().ToList();

        var customerList = await customers.ListAsync(c => customerIds.Contains(c.Id), ct);
        var categoryList = await categories.ListAsync(c => categoryIds.Contains(c.Id), ct);

        var customerMap = customerList.ToDictionary(c => c.Id);
        var categoryMap = categoryList.ToDictionary(c => c.Id);

        var assigneeIds = pagedTickets.Select(t => t.AssigneeId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var assigneeMap = new Dictionary<Guid, string>();
        foreach (var aId in assigneeIds)
        {
            var assignee = await identityUsers.FindByIdAsync(aId, ct);
            assigneeMap[aId] = assignee?.FullName ?? string.Empty;
        }

        var items = pagedTickets.Select(t => new TicketListItemDto(
            t.Id,
            t.Reference,
            t.Subject,
            t.Status,
            t.Priority,
            t.CustomerId,
            customerMap.TryGetValue(t.CustomerId, out var cust) ? cust.Name : string.Empty,
            t.CategoryId,
            categoryMap.TryGetValue(t.CategoryId, out var cat) ? cat.Name : string.Empty,
            t.AssigneeId,
            t.AssigneeId.HasValue ? assigneeMap.GetValueOrDefault(t.AssigneeId.Value, string.Empty) : null,
            t.CreatedAt,
            t.EscalationState,
            t.FirstResponseAt,
            t.LastResponseAt,
            t.ResolvedAt,
            t.ClosedAt,
            t.EscalationAssigneeId)).ToList();

        return Response<PaginatedList<TicketListItemDto>>.Ok(
            PaginatedList<TicketListItemDto>.Create(items, total, pageIndex, pageSize),
            SystemCodeMap.Resolve("SUCCESS_OPERATION"), "OK");
    }
}
