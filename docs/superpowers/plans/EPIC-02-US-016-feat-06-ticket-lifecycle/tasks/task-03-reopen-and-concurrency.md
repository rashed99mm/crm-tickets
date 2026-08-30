# Task 3 — Reopen, and refuse lost updates

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — tasks 4.1–4.4 |
| Feature | `FEAT-06` Ticket detail and lifecycle |
| Criteria | `AC-40`, `AC-41` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Domain/Interfaces/IRepository.cs` (`SetOriginalValue`)
- `src/CustomerSupport.Infrastructure/Persistence/BaseRepository.cs`
- `src/CustomerSupport.Domain/Entities/Tickets/TicketHistory.cs` (the `Id` fix — see below)
- `src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommand.cs`

## Test evidence

- `AC40_Reopen_PersistsAndRecordsAReopenRow` — from `Resolved` and from `Closed`, each asserting the
  row is `Reopened` rather than `StatusChanged`
- `AC41_ConcurrentStatusChange_SecondCallerGets409AndFirstChangeSurvives`
- `AC41_ChangeStatus_WithoutRowVersion_Returns400`

Against real SQL Server — `US-026` TC-03 requires it, and the in-memory provider does not honour
`rowversion`. Suite: **233 passed, 0 failed.**

## The bug this task uncovered, which was not in this task

Every status change returned **500**, and the cause was two pieces of Phase 0 colliding.

`TicketHistory.Record` assigned `Id = Guid.NewGuid()`. When a row is appended to an
**already-tracked** ticket, EF decides Added-versus-Modified by asking whether the primary key is
set — so a client-assigned Guid made a brand-new row look like an existing one, EF marked it
`Modified`, and `GuardAppendOnlyHistory` (ADR-0010) correctly refused to save a modified history row.

The creation path never hit it: there the whole graph is new, and an `Added` parent makes its
children `Added` whatever their keys hold. **The defect was therefore invisible until the first
mutation of an existing ticket** — which is this feature, three phases after the code was written.

Fixed by leaving `Id` unset and letting EF generate it, with the reasoning written into the entity
so the next person to "tidy up" that missing assignment does not reintroduce it.

What it says about ADR-0010: the guard worked exactly as designed and still produced a false
refusal, because it cannot tell a mis-tracked insert from a genuine mutation. Nothing in the ADR
anticipated that, and it is worth knowing before the next entity gets the same treatment.

## Why `AC-41` needs the client to echo a version

A `rowversion` column alone does not produce a conflict across two HTTP requests: each loads the
ticket fresh, sees the current value, and saves successfully. The criterion's "two callers changed
the same ticket" never materialises, and the column is decoration.

So the version travels: the detail read returns it, the mutation echoes it, and
`SetOriginalValue` applies it as the tracked entity's original value so EF compares against **what
the caller actually saw**. The test reads one version, uses it twice, and asserts the second attempt
is refused and the first change survives.

## Deviations from the plan

**1. `SetOriginalValue` is generic, not `SetOriginalRowVersion`.**
First written as a ticket-specific method on a generic interface, which was the wrong shape.
Concurrency tokens are not unique to tickets, and the repository should not learn a domain type's
property names.

**2. Malformed base64 is rejected by the validator, not the handler.**
`Convert.FromBase64String` on a malformed value throws `FormatException`, which would surface as a
500 — `AC-52`'s problem as much as this criterion's. `Convert.TryFromBase64String` in the validator
turns it into a 400.
