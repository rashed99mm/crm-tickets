# FEAT-06 — Ticket detail and lifecycle · backend task record

**Plan:** [`implementation-plan/implementation-plan.md`](./implementation-plan.md)
**Executed:** 2026-08-26
**Status:** delivered

## Evidence

```
dotnet build CustomerSupport.slnx    → Build succeeded. 0 Warning(s), 0 Error(s)
dotnet test CustomerSupport.slnx
Passed!  - Failed: 0, Passed: 233, Skipped: 0, Total: 233, Duration: 58 s
```

## Tasks

| # | Task | Criteria | Commit | Status |
|---|---|---|---|---|
| [01](./tasks/task-01-status-change-endpoint.md) | The status-change command and endpoint | AC-35, AC-37, AC-36 | uncommitted | `done` |
| [02](./tasks/task-02-refuse-undefined-transitions.md) | Refuse undefined transitions, and tell 409 from 400 | AC-38, AC-39, AC-30 | uncommitted | `done` |
| [03](./tasks/task-03-reopen-and-concurrency.md) | Reopen, and refuse lost updates | AC-40, AC-41 | uncommitted | `done` |

## Criteria delivered

| `AC-n` | Test naming it |
|---|---|
| AC-35 | `AC35_GetTicket_ReturnsCustomerSummaryAndHistoryNewestFirst` |
| AC-36 | `AC36_ChangeStatus_UnknownTicket_Returns404`, plus `AC36_GetTicket_UnknownId_Returns404` from FEAT-04 |
| AC-37 | `AC37_ChangeStatus_PermittedTransition_Returns200AndPersists` (6 cases) |
| AC-38 | `AC38_ChangeStatus_UndefinedTransition_Returns409NotValidationError` (3 cases), `AC38_RefusedTransition_ChangesNothing` |
| AC-39 | `AC39_ChangeStatus_ToTheStatusAlreadyHeld_Returns409` (3 cases) |
| AC-40 | `AC40_Reopen_PersistsAndRecordsAReopenRow` (2 cases) |
| AC-41 | `AC41_ConcurrentStatusChange_SecondCallerGets409AndFirstChangeSurvives`, `AC41_ChangeStatus_WithoutRowVersion_Returns400` |
| AC-30 | `AC30_ChangeStatus_UnknownStatusValue_Returns400NotConflict` |

## Deviations from the plan

**D1 — A client-assigned `Id` on an appended history row made EF mark it `Modified`, and the
ADR-0010 guard then refused a perfectly legitimate append.**

This was the hard bug of the phase. Every status change returned **500**, and the cause was code
written in Phase 0 interacting with a guard written in Phase 0:

`TicketHistory.Record` set `Id = Guid.NewGuid()`. When a row is appended to an **already-tracked**
ticket, EF discovers it during change detection and decides Added-versus-Modified by asking whether
the primary key is already set. A client-assigned Guid makes a brand-new row look like an existing
one, so EF marked it `Modified` — and `GuardAppendOnlyHistory` correctly refused to save a modified
history row.

The creation path never hit this because there the whole graph is new: an `Added` parent makes its
children `Added` regardless of their keys. **So the bug was invisible until the first mutation of an
existing ticket**, which is exactly this feature.

Fixed by not assigning `Id` in `Record` and letting EF generate it. The reasoning is in the entity,
because the next person to "tidy up" that missing assignment will reintroduce the bug.

**Worth noting what this says about ADR-0010:** the guard did its job — it refused a write that
would have altered a history row. It just could not tell a mis-tracked insert from a real mutation,
and nothing in the ADR anticipated that failure mode.

**D2 — Diagnosis needed a throwaway probe, again.**
The 500 arrived as the generic envelope with no detail, and sending the command through MediatR in a
bare scope returned `TICKET_NOT_ASSIGNED_TO_YOU` instead — because there is no HTTP user in that
scope, so `IUserContext.UserId` is empty and the authorization check short-circuits first. The
actual exception only surfaced by exercising the DbContext directly and printing
`ChangeTracker.Entries<TicketHistory>()` states. Second time this phase pattern has been needed.

**D3 — `IRepository<T>.SetOriginalValue` was added.**
`AC-41` needs the caller's version applied as the tracked entity's original value, and the
repository had no way to express that. Added as a generic property-level method rather than a
ticket-specific one, with the reasoning on the interface.

**D4 — The status endpoint is `POST /{id}/status`, not `PATCH /{id}`.**
Planned and worth restating as delivered: a status change is a **transition**, not a field
assignment. `PATCH { "status": "Closed" }` invites a client to think it is setting a value, and the
transition table refuses that reading.

## Accepted risks

**`AC-41` depends on the client echoing `rowVersion`.** A client that omits it gets a 400
(`AC41_ChangeStatus_WithoutRowVersion_Returns400`), and one that invents a value gets a 409 — but a
client that caches a version across a reload could produce a puzzling conflict. That is the correct
outcome and the message says so; it is recorded because it is a real usability edge.
