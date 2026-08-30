# FEAT-07 — Assignment and per-record authorization · task record

**Plan:** [`implementation-plan/implementation-plan.md`](./implementation-plan.md)
**Executed:** 2026-08-26
**Status:** delivered — and it closed two Day 1 `partial` stories

## Evidence

```
dotnet test CustomerSupport.slnx
Passed!  - Failed: 0, Passed: 233, Skipped: 0, Total: 233, Duration: 58 s
```

## Tasks

| # | Task | Criteria | Commit | Status |
|---|---|---|---|---|
| [01](./tasks/task-01-role-vocabulary.md) | Seed `Agent` and `Supervisor`, add the policies | ADR-0012, A2 | uncommitted | `done` |
| [02](./tasks/task-02-assign-ticket.md) | A supervisor assigns work | AC-42, AC-44 | uncommitted | `done` |
| [03](./tasks/task-03-per-record-authorization.md) | An agent may only move their own ticket | AC-43, AC-45, AC-46, AC-47 | uncommitted | `done` |
| [04](./tasks/task-04-close-day-one-partials.md) | Finish `US-035` and `US-013` | AC-33, AC-34 | uncommitted | `done` |

## Criteria delivered

| `AC-n` | Test naming it |
|---|---|
| AC-42 | `AC42_Supervisor_AssignsUnassignedTicket_Returns200`, `AC42_Supervisor_ReassignsTicket_RecordsReassignedWithPreviousHolder` |
| AC-43 | `AC43_Agent_AssigningAnyTicket_Returns403`, `AC43_Agent_AssigningTheirOwnTicket_StillReturns403` |
| AC-44 | `AC44_Assign_UnknownTargetUser_Returns400KeyedToAssigneeId`, `AC44_Assign_TargetIsNotAnAgent_Returns400` |
| AC-45 | `AC45_Agent_ChangingAnotherAgentsTicket_Returns403AndTicketUnchanged`, `AC45_Agent_ChangingAnUnassignedTicket_Returns403` |
| AC-46 | `AC46_Agent_ChangingTheirOwnTicket_Returns200` |
| AC-47 | `AC47_Supervisor_ChangingAnyTicket_Returns200` |
| AC-33 | `AC33_GetTickets_AssigneeFilter_ReturnsOnlyThatAgentsTickets` — the last of AC-33's four filters |
| AC-34 | `AC34_GetTickets_MineReturnsOnlyTicketsAssignedToTheCaller` — AC-34's positive half |

## The argument this feature exists to demonstrate

`AC-45` and `AC-46` differ **only by which ticket is addressed**. Same caller, same role, same
endpoint, same verb — one is 200 and the other 403. No endpoint policy can express that, because the
answer is not knowable until the ticket is loaded, which is after the policy has already run.

`AC-43` is the mirror image and is a *pure* policy check: an agent may not assign, whatever the
ticket. Putting it in the handler would make the refusal depend on a branch nobody is forced to
write.

Both layers, each doing the job the other cannot. That is the spec's design section, and it is what
the two sets of tests above prove separately.

## Deviations from the plan

**D1 — A role-seeding race broke the whole suite before any of these tests ran.**
`IdentitySeeder` now seeds `Agent` and `Supervisor` on every host start, and `CrmApiFactory` also
creates roles on demand. Test classes run in parallel, each starts a host, and the check-then-create
collided on `RoleNameIndex` — failing tests in files that have nothing to do with this feature. The
factory now tolerates the duplicate, the same treatment `CategorySeeder` needed in Day 1. Third
instance of this pattern; it is a property of the parallel-host fixture, not of any one seeder.

**D2 — `AC45_Agent_ChangingAnUnassignedTicket_Returns403` is not in the story's test cases.**
Added deliberately. An unassigned ticket belongs to nobody, so an implementation that inverted the
ownership check — or read `null` as "anyone" — would pass every other test here and hand every agent
every unassigned ticket. The story's four cases do not cover it.

**D3 — `AC-44` needed a role lookup, not an existence check.**
The plan said so and it is worth restating as delivered: a supervisor is a real user with a real id,
so `FindByIdAsync` alone would accept one as an assignee. The handler asks Identity for the target's
roles and refuses anything that is not an `Agent`.
