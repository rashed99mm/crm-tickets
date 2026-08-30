# Task 3 — The "my tickets" filter

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — US-035, tasks 2.1–2.3 |
| Feature | `FEAT-05` Ticket queue |
| Criteria | `AC-34` |
| Status | `partial` — see below |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/GetTicketsQuery.cs`
- `src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` (`mine` parameter)
- `tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

## Test evidence

- `AC34_GetTickets_MineIgnoresSuppliedAssigneeId`
- `AC34_GetTickets_MineWithNoTickets_Returns200EmptyPage`

Suite: **193 passed, 0 failed.**

## Why this task is `partial`

**`AC-34`'s positive case is not proven.** "Given the caller is an agent, when listing with the
'mine' filter, then only tickets assigned to that caller" needs a ticket **assigned** to someone, and
no assignment endpoint exists until `FEAT-07`. The negative case (a caller who owns nothing gets an
empty page) and the security case (a supplied `assigneeId` cannot widen the result) are both proven;
the middle one cannot be constructed yet.

`US-035` is marked `partial` and its `TC-01` says exactly this. The filter is implemented and the
caller id is correctly taken from the token — what is missing is the fixture, not the code, and that
distinction is recorded rather than rounded up.

## The test that matters

`AC34_GetTickets_MineIgnoresSuppliedAssigneeId` is **a security test wearing a filter's clothes.**

The handler resolves the assignee from `IUserContext.UserId` when `mine` is set and ignores any
`assigneeId` in the query string. Had it honoured both — merged them, or let the explicit id win —
then `mine=true&assigneeId=<someone else>` would return that person's queue. The toggle would be an
information-disclosure endpoint with a friendly name, and it would look completely reasonable in
review.

Same rule as `AC-19`'s note author: **the actor comes from the token, never from the request.**

## Deviations from the plan

None. Tasks 2.1–2.3 landed as written.

## Scope note, so this is not over-read

`AC-34` scopes a **list**. Listing another agent's tickets by explicit `assigneeId` remains
permitted — the queue is shared work and no criterion in this slice restricts reading it. Per-record
authorization governs **mutation** and arrives with `FEAT-07` as `AC-45` and `AC-46`. Treating this
task as if it had delivered that would misrepresent the security posture.
