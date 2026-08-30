# Task 1 — The paged queue, newest first

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — tasks 1.1, 1.2 |
| Feature | `FEAT-05` Ticket queue |
| Criteria | `AC-32` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/GetTicketsQuery.cs`
- `src/CustomerSupport.Application/Features/Tickets/Dtos/TicketDtos.cs` (`TicketListItemDto`)
- `src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` (`GetAll`)
- `tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

## Test evidence

`AC32_GetTickets_ReturnsPagedNewestFirst` — creates two tickets 20 ms apart and asserts the newer one
appears at a lower index. Suite: **193 passed, 0 failed.**

## Deviations from the plan

**1. The handler does not use `GetPagedAsync`.**
The plan assumed the generic paged helper. The list needs `customerName` and `categoryName`, which
live in other tables, so the handler composes an explicit `join` across the ticket, customer and
category queryables and pages it by hand — count, then `OrderByDescending` / `Skip` / `Take`, then
project.

Resolving the two names per row instead would have been the classic N+1: invisible at the two rows a
test creates, obvious at fifty. It is the kind of thing that never fails a test and always fails in
use.

**2. `TicketListItemDto` carries no description.**
Not stated in the plan beyond "list item". A 4000-character body per row turns a 50-row page into a
payload nobody reads, and the queue never renders it.

## The point of this task

**Newest-first is the default ordering, not an option.** `OrderByDescending(CreatedAt)` applies
unless `SortBy` explicitly overrides it. A queue whose order depends on an unsupplied query parameter
is a queue whose order is undefined, and the first person to notice would be an agent wondering why
their list reshuffles.

## Accepted risk

`AC32_…ReturnsPagedNewestFirst` orders by `CreatedAt` on rows created 20 ms apart. `datetime2` has
ample precision for that, but the test is timing-dependent in principle. It filters by `customerId`
so its assertion is scoped to its own two rows rather than to whatever else the shared test database
happens to hold — without that, other test classes running in parallel would make it flaky.
