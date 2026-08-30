# FEAT-05 Ticket Queue (backend) Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Goal:** List tickets, newest first, with combinable filters — status, priority, assignee, customer, "mine", "unassigned (`AC-32`..`AC-34`, `AC-82`).

**Spec:** `docs/superpowers/specs/EPIC-02-US-016-ticket-lifecycle.md` (`AC-32`..`AC-34`, `AC-82`).

**Architecture:** `GetTicketsQueryHandler` (Application) → `TicketsController.GetAll`. Filters compose into one `PredicateBuilder` predicate; assignee names and customer/category names are resolved in memory (small result set) and projected into `TicketListItemDto`.

## Global constraints

- Filters compose: `status` + `priority` narrow to the intersection (`AC-33`).
- An unknown `status`/`priority` value is a 400, not an empty page (an empty page would read as "nothing in that state").
- `mine` resolves to the caller's id from the token (`AC-34`); `unassigned` means `AssigneeId == null` (distinct from omitting `assigneeId`).

## Task 1 — `GetTicketsQueryHandler` (`AC-32`, `AC-33`)

**Files:** `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/{GetTicketsQuery,GetTicketsQueryHandler,GetTicketsQueryValidator}.cs`

**Interfaces:** `GetTicketsQuery { int PageIndex, int PageSize, string? Status, string? Priority, Guid? CustomerId, Guid? AssigneeId, bool Mine, bool Unassigned }`.

**Step 1 — Real handler (excerpt)**

```csharp
// backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/GetTicketsQueryHandler.cs
public async Task<Response<PaginatedList<TicketListItemDto>>> Handle(GetTicketsQuery request, CancellationToken ct)
{
    var assigneeId = request.Mine ? userContext.UserId : request.AssigneeId;
    var filter = PredicateBuilder.True<Ticket>()
        .WhereIf(!string.IsNullOrWhiteSpace(request.Status), t => t.Status == request.Status!)
        .WhereIf(!string.IsNullOrWhiteSpace(request.Priority), t => t.Priority == request.Priority!)
        .WhereIf(request.CustomerId.HasValue, t => t.CustomerId == request.CustomerId!.Value)
        .WhereIf(request.Unassigned && !request.Mine, t => t.AssigneeId == null)
        .WhereIf(assigneeId.HasValue, t => t.AssigneeId == assigneeId!.Value);

    var total = await tickets.CountAsync(filter, ct);
    var page = await tickets.ListProjectedOrderedAsync(filter,
        t => new { t.Id, t.Reference, t.Subject, t.Status, t.Priority, t.CustomerId, t.CategoryId, t.AssigneeId, t.CreatedAt, t.EscalationState },
        t => t.CreatedAt, descending: true, ct);

    var paged = page.Skip((Math.Max(request.PageIndex,1)-1) * Math.Max(request.PageSize,1)).Take(request.PageSize).ToList();
    // resolve customer/category/assignee names in memory, then project to TicketListItemDto
    var customerMap = (await customers.ListAsync(c => customerIds.Contains(c.Id), ct)).ToDictionary(c => c.Id);
    var categoryMap = (await categories.ListAsync(c => categoryIds.Contains(c.Id), ct)).ToDictionary(c => c.Id);
    foreach (var aId in assigneeIds) assigneeMap[aId] = (await identityUsers.FindByIdAsync(aId, ct))?.FullName ?? string.Empty;

    return Response<PaginatedList<TicketListItemDto>>.Ok(
        PaginatedList<TicketListItemDto>.Create(items, total, pageIndex, pageSize),
        SystemCodeMap.Resolve("SUCCESS_OPERATION"), "OK");
}
```

`ESC_state` is already on the ticket (`EscalationState`), so the queue can sort/pill without a second query.

- [ ] **Step 2: Run — integration test**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~GetTicketsQueryTests"`
Expected: PASS — newest first; `status=Open&priority=High` intersects; `mine=true` filters to caller.

- [ ] **Step 3: Commit:** `git add backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/ && git commit -m "feat(queue): GetTickets combinable filters (AC-32, AC-33)"`

## Task 2 — Controller list + `mine`/`unassigned` (`AC-34`, `AC-82`)

**Files:** `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` (`GetAll`)

**Step 1 — Real controller (excerpt)**

```csharp
[HttpGet]
[ProducesResponseType(typeof(Response<PaginatedList<TicketListItemDto>>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Response<TicketListItemDto>), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> GetAll(
    [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
    [FromQuery] string? status = null, [FromQuery] string? priority = null,
    [FromQuery] Guid? customerId = null, [FromQuery] Guid? assigneeId = null,
    [FromQuery] bool mine = false, [FromQuery] bool unassigned = false, CancellationToken ct = default)
{
    var result = await mediator.Send(new GetTicketsQuery {
        PageIndex = page, PageSize = pageSize, Status = status, Priority = priority,
        CustomerId = customerId, AssigneeId = assigneeId, Mine = mine, Unassigned = unassigned,
    }, ct);
    return this.ToActionResult(result);
}
```

`mine` is resolved from the token inside the handler (only it has the principal); `unassigned` is ignored when `mine` is set (the handler guards `request.Unassigned && !request.Mine`).

- [ ] **Step 2: Run:** `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketQueueEndpointTests"`
Expected: PASS — `mine=true` returns only the caller's tickets; `unassigned=true` returns the unassigned queue.

- [ ] **Step 3: Commit:** `git add backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs && git commit -m "feat(queue): list endpoint, mine/unassigned (AC-34, AC-82)"`

## Self-review

Coverage: `AC-32`,`AC-33` → Task 1; `AC-34`,`AC-82` → Task 2.

**Discrepancy found:** the old plan said "unknown status/priority → 400". The shipped `GetTicketsQueryValidator` enforces this — matches; the rewrite states the validator exists rather than assuming the controller throws.
