# Task 2 — Per-record authorization: an agent may only move their own ticket

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — US-119 and US-120, tasks 2.1–3.4 |
| Feature | `FEAT-07` Assignment and per-record authorization |
| Criteria | `AC-43`, `AC-45`, `AC-46`, `AC-47` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommand.cs`
- `src/CustomerSupport.InternalApi/Controllers/TicketsController.cs`
- `src/CustomerSupport.Api.Shared/Extensions/AuthorizationExtensions.cs`

## Test evidence

- `AC43_Agent_AssigningAnyTicket_Returns403`
- `AC43_Agent_AssigningTheirOwnTicket_StillReturns403`
- `AC45_Agent_ChangingAnotherAgentsTicket_Returns403AndTicketUnchanged`
- `AC45_Agent_ChangingAnUnassignedTicket_Returns403`
- `AC46_Agent_ChangingTheirOwnTicket_Returns200`
- `AC47_Supervisor_ChangingAnyTicket_Returns200`

Suite: **233 passed, 0 failed.**

## Two layers, each doing what the other cannot

**`AC-43` is a pure policy check.** An agent may not assign, whatever the ticket — so it sits on the
endpoint as `[Authorize(Policy = "Supervisor")]`. Moving it into the handler would make the refusal
depend on a branch someone has to remember to write.

**`AC-45` and `AC-46` cannot be a policy at all.** They differ only by *which ticket* was addressed:
same caller, same role, same endpoint, same verb, and one is 200 while the other is 403. That is not
knowable until the ticket is loaded, which happens after the policy has already run. So the check is
in the handler, over `Ticket.IsAssignedTo` — which existed and was unit-tested from Phase 0, with no
database involved.

This split is the spec's design section, and the two sets of tests prove it separately rather than
as one blurred whole.

## The two tests worth the most

**`AC43_Agent_AssigningTheirOwnTicket_StillReturns403`** — the parenthetical in `AC-43`, and the
case a "reasonable" ownership shortcut gets wrong. A handler that let a caller act on their own
ticket would permit this, and it would read as sensible in review. Permission precedes ownership:
assignment is a supervisory act regardless of who currently holds the ticket.

**`AC45_Agent_ChangingAnUnassignedTicket_Returns403`** — not in `US-120`'s test cases, added
deliberately. An unassigned ticket belongs to nobody, so an implementation that inverted the check
or treated `null` as "anyone" would pass every other test in this task and quietly hand every agent
every unassigned ticket. The story's four cases all use assigned tickets.

## Deviations from the plan

**1. `AC45_…Returns403AndTicketUnchanged` re-fetches.**
The status code alone would pass even if the handler had mutated the entity and then refused to
report success. The assertion that the status is still `New` is what proves the refusal was total.

**2. The supervisor override reads `Supervisor` **or** `Admin`.**
Matching the policy from ADR-0012, so an administrator is not locked out. Written as
`HasAnyRole(Supervisor, Admin)` rather than duplicating the policy's role list in prose.

## Ordering note, now discharged

`AC-45`'s test needs a ticket assigned to *another* agent, so this task could not run before
`US-014`. That same dependency is why Day 1 left `US-035` and `US-013` at `partial` — closed in
[task 4](./task-04-close-day-one-partials.md).
