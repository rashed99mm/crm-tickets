# Task 1 — A supervisor assigns work

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — US-014, tasks 1.1–1.5 |
| Feature | `FEAT-07` Assignment and per-record authorization |
| Criteria | `AC-42`, `AC-44` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Tickets/Commands/AssignTicket/AssignTicketCommand.cs`
- `src/CustomerSupport.Application/Features/Tickets/Validators/TicketCommandValidators.cs`
- `src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` (`Assign`)

## Test evidence

- `AC42_Supervisor_AssignsUnassignedTicket_Returns200`
- `AC42_Supervisor_ReassignsTicket_RecordsReassignedWithPreviousHolder` — asserts the history row is
  `Reassigned` and carries the previous holder, not just that the assignee changed
- `AC44_Assign_UnknownTargetUser_Returns400KeyedToAssigneeId`
- `AC44_Assign_TargetIsNotAnAgent_Returns400`

Suite: **233 passed, 0 failed.**

## The test that would have been easy to get wrong

`AC44_Assign_TargetIsNotAnAgent_Returns400`. An existence check passes here — the target is a
supervisor, a real user with a real id — so a handler that only called `FindByIdAsync` would accept
it and the test would fail for the right reason.

Without that check the endpoint would cheerfully assign support tickets to the knowledge-base
editor, and nothing downstream would notice: the ticket would simply sit in a queue its assignee
never looks at.

The handler asks Identity for the target's roles and refuses anything without `Agent`. The picker
added for `US-128` applies the same filter, so it cannot offer a value the mutation would reject.

## Deviations from the plan

**1. `AC-43` is not enforced in this handler, deliberately.**
That an agent may not assign at all does not depend on which ticket was addressed, so it belongs on
the endpoint as `[Authorize(Policy = "Supervisor")]`. Putting it in the handler would make the
refusal depend on a branch nobody is forced to write — and would blur the distinction this feature
exists to demonstrate.

**2. Both `AC-44` failures are keyed to `AssigneeId` and typed `Validation`.**
So they arrive as a field-keyed 400 and land on the picker that caused them, the same rule as
`AC-31`: the target is named in the **body**, and the addressed resource — the ticket — exists.

**3. Assignment also carries `rowVersion`.**
Not required by `AC-42`, and included because assignment mutates the same aggregate as a status
change. Omitting it would leave a write path that silently overwrites a concurrent edit, which is
the exact failure `AC-41` forbids one endpoint away.

## What the aggregate decided, not this handler

`Assigned` versus `Reassigned`, and the refusal of a no-op reassignment, are `Ticket.AssignTo`'s
calls — unit-tested in Phase 0 and unchanged here. The handler's job is the target's validity and
the persistence, and it should not be able to record a different history row than the aggregate
chose.
