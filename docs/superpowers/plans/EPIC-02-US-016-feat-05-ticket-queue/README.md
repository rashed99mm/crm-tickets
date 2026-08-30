# FEAT-05 — Ticket queue · backend task record

**Plan:** [`implementation-plan/implementation-plan.md`](./implementation-plan.md)
**Executed:** 2026-08-26
**Status:** delivered, with `US-013` and `US-035` at `partial` - AC-33's assignee filter and
AC-34's positive case both need a ticket that is actually assigned, which is impossible until
FEAT-07 exists. Detail in tasks 02 and 03.

## Evidence

```
dotnet test CustomerSupport.slnx
Passed!  - Failed: 0, Passed: 193, Skipped: 0, Total: 193, Duration: 28 s
```

## Tasks

| # | Task | Criteria | Commit | Status |
|---|---|---|---|---|
| [01](./tasks/task-01-list-tickets.md) | The paged queue, newest first | AC-32 | uncommitted | `done` |
| [02](./tasks/task-02-filters.md) | Filters that combine, and refuse nonsense | AC-33, AC-11 | uncommitted | `partial` — assignee filter untestable until FEAT-07 |
| [03](./tasks/task-03-mine-filter.md) | The "my tickets" filter | AC-34 | uncommitted | `partial` — positive case needs FEAT-07 |

## Criteria delivered

| `AC-n` | Test naming it |
|---|---|
| AC-32 | `AC32_GetTickets_ReturnsPagedNewestFirst` |
| AC-33 | `AC33_GetTickets_EachFilter_ReturnsOnlyMatching`, `AC33_GetTickets_CombinedFilters_NarrowToIntersection`, `AC33_GetTickets_UnknownStatusValue_Returns400` |
| AC-34 | `AC34_GetTickets_MineWithNoTickets_Returns200EmptyPage`, `AC34_GetTickets_MineIgnoresSuppliedAssigneeId` |
| AC-11 | `AC11_GetTickets_PageSizeAboveMaximum_Returns400` |

## The two tests that carry weight

**`AC33_…CombinedFilters_NarrowToIntersection`.** Each filter passing in isolation says nothing about
whether they compose. A handler that overwrote the predicate instead of conjoining it would pass
every single-filter test and fail every real use. The test asserts the combined result is strictly
smaller than one of the single-filter results, which only holds if `WhereIf` genuinely conjoins.

**`AC34_…MineIgnoresSuppliedAssigneeId`.** A security test wearing a filter's clothes. The handler
resolves the assignee from `IUserContext.UserId` when `mine` is set and ignores any `assigneeId` in
the query string. Had it honoured both, `mine=true&assigneeId=<someone else>` would have returned
that person's queue — an information-disclosure endpoint with a friendly name.

Note the scope: `AC-34` scopes a **list**, and listing another agent's tickets by explicit
`assigneeId` stays permitted. The queue is shared work and no criterion in this slice restricts
reading it. Per-record authorization governs **mutation** and arrives in `FEAT-07` (`AC-45`,
`AC-46`).

## Deviations from the plan

**D1 — `GetTicketsQueryHandler` does not use `GetPagedAsync`.**
The plan assumed the generic paged helper. The list needs `customerName` and `categoryName`, which
live in other tables, so the handler composes an explicit `join` across the ticket, customer and
category queryables and pages it by hand. Resolving the names per row would have been the classic
N+1 — invisible at two rows, obvious at fifty.

**D2 — An unknown `status` or `priority` is refused rather than matched against nothing.**
Planned as task 1.5 and worth restating as delivered, because the alternative failure is silent: a
typo'd filter returning an empty page reads as "no tickets in that state", which is
indistinguishable from the truth and impossible to debug from the UI. `GetTicketsQueryValidator`
checks both against the value objects, so adding a status means editing one file rather than every
validator that happens to list them.

**D3 — Newest-first is the default ordering, not an option.**
`OrderByDescending(CreatedAt)` unless `SortBy` overrides. A queue whose order depends on an
unsupplied query parameter is a queue whose order is undefined.

## Accepted risks

`AC32_GetTickets_ReturnsPagedNewestFirst` orders by `CreatedAt`, which the test creates 20 ms apart.
`datetime2` has ample precision for that, but the test is nonetheless timing-dependent in principle.
It filters by `customerId` so the assertion is scoped to its own two rows rather than to whatever
else the shared test database holds.
