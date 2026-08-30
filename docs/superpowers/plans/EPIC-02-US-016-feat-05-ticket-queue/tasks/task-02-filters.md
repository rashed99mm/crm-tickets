# Task 2 — Filters that combine, and refuse nonsense

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — tasks 1.3–1.6 |
| Feature | `FEAT-05` Ticket queue |
| Criteria | `AC-33`, `AC-11` |
| Status | `partial` — see below |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/GetTicketsQuery.cs`
- `src/CustomerSupport.Application/Features/Tickets/Validators/TicketValidators.cs` (`GetTicketsQueryValidator`)
- `tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

## Test evidence

- `AC33_GetTickets_EachFilter_ReturnsOnlyMatching`
- `AC33_GetTickets_CombinedFilters_NarrowToIntersection`
- `AC33_GetTickets_UnknownStatusValue_Returns400`
- `AC11_GetTickets_PageSizeAboveMaximum_Returns400`

Suite: **193 passed, 0 failed.**

## Why this task is `partial`

**The assignee filter is implemented but untested in isolation.** `AC-33` names four filters —
status, priority, assignee, customer — and nothing can assign a ticket until `FEAT-07`, so no fixture
can put a ticket in a state where filtering by assignee returns a non-empty result. The predicate is
there and `mine` exercises the same column, but that is an argument, not a test.

`US-013` is marked `partial` for this reason and `TC-02` says so. Rounding it up to `done` would be
the exact failure `CLAUDE.md` describes: reporting work complete on a branch that has not been
executed.

## The test that carries the criterion

`AC33_GetTickets_CombinedFilters_NarrowToIntersection`. Each filter passing in isolation says
**nothing** about whether they compose — a handler that overwrote the predicate instead of conjoining
it would pass every single-filter test and fail every real use, because real use is almost always two
filters at once.

The test asserts the combined count is strictly smaller than one of the single-filter counts, which
holds only if `WhereIf` genuinely conjoins.

## Deviations from the plan

**1. Invalid filter values are rejected by the validator, using the value objects.**
Task 1.5 asked for the behaviour; the implementation reads `TicketStatus.TryCreate` and
`TicketPriority.TryCreate` rather than restating the five and four values. One source, so adding a
status means editing one file rather than one file and every validator that happens to list them.

## The point of this task

An unknown status **must** be refused rather than matched against nothing, because the alternative
failure is silent. A typo'd filter returning an empty page reads to the user as "no tickets in that
state" — indistinguishable from the truth, and impossible to debug from the UI. A 400 naming the
`Status` field is the only version of this the agent can act on.
