# Task 3 — Close the two Day 1 `partial` stories

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — *Ordering note* |
| Feature | `FEAT-07`, discharging `FEAT-05` debt |
| Criteria | `AC-33` (assignee filter), `AC-34` (positive half) |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `tests/CustomerSupport.Tests/Integration/TicketLifecycleEndpointTests.cs`
- `docs/requirements/user-stories/US-035-agent-sees-own-work.md`
- `docs/requirements/user-stories/US-013-filter-the-queue.md`

## Test evidence

- `AC34_GetTickets_MineReturnsOnlyTicketsAssignedToTheCaller` — two tickets assigned to two
  different agents; the caller sees theirs and not the other
- `AC33_GetTickets_AssigneeFilter_ReturnsOnlyThatAgentsTickets`

Suite: **233 passed, 0 failed.** Both stories move from `partial` to `done`.

## What was actually missing, and what was not

Nothing was wrong with the Day 1 code. `GetTicketsQueryHandler` already filtered by assignee and
already resolved `mine` from the token — both written and reviewed in Phase 2.

What was missing was a **fixture**. `AC-34`'s positive half needs a ticket that is genuinely
assigned to the caller, and no endpoint could assign one until `US-014` landed today. The same
dependency left `AC-33`'s fourth filter untestable in isolation.

So Day 1 recorded them as `partial` with the reason, rather than rounding up to `done` on the
strength of the code being visibly correct. This task is what makes that honest bookkeeping pay off:
the moment the dependency existed, two tests closed both gaps.

## Why the tests live in `TicketLifecycleEndpointTests`

They assert `FEAT-05` criteria but need `FEAT-07`'s fixture — supervisor plus two agents plus the
assign endpoint. Duplicating that setup into `TicketEndpointTests` would mean maintaining it twice.
Named `AC34_…` and `AC33_…` so a traceability search from either criterion still finds them,
wherever they sit.

## The general point

This is the second time reading a story's test-case table found something the code review did not —
the first was Day 1's missing `AC12_UpdateCustomer_UnknownId_Returns404`. The tables are not
paperwork; they are the only artefact that tracks *which parts of a criterion are proven*, as
opposed to which criteria have some test attached.
