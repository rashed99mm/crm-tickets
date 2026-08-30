# US-026 · Reopen a ticket, and never lose a concurrent change

| Field | Value |
|---|---|
| **Story** | `US-026` *(was `US-1.27`)* — rule proposal: *Reopen Ticket* |
| **Epic** | [EPIC-02 Ticket management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-06` Ticket detail and lifecycle](../delivery-plan.md#feat-06--ticket-detail-and-lifecycle) |
| **Layer** | Backend |
| **Ships with** | [US-128](./US-128-ticket-detail-with-guarded-actions.md) *(frontend)* |
| **Actor** | Support Agent |
| **Priority** | P1 |
| **Sprint** | [3 — Ticket detail, lifecycle, assignment and history](../delivery-plan.md#sprint-3--ticket-detail-lifecycle-assignment-and-history) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-2.10, FR-2.11, BR-13, BR-18 |
| **Spec criteria** | AC-40, AC-41 |
| **Depends on** | [US-016](./US-016-move-along-the-lifecycle.md) |

## Story

**As an agent**, **I want** to reopen a ticket the customer says is not fixed, and to be told if someone else changed it while I was looking, **so that** no update is silently overwritten.

## Business rules

- BR-13 — conflicting concurrent change refuses later write, earlier survives (BRD).
- BR-18 — reopen begins new resolution period, original retained (BRD).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Reopen recorded in history (spec AC-40)

Given a resolved or closed ticket, when reopening, then status becomes `In Progress` and the reopen is
recorded in history.

#### AC2 — Concurrent change refuses later write (spec AC-41)

Given two callers changing the same ticket concurrently, then the second receives 409 and the
first change survives. No silent overwrite.

## SQL tables

Concurrency and reopen — from the [S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#tickets):

```sql
[RowVersion] ROWVERSION NOT NULL   -- EF maps to a concurrency token; conflict → 409 (ERR024)
-- A successful reopen appends: TicketHistory(ChangeType='Reopened',
--   FromValue='Resolved'|'Closed', ToValue='In Progress', OccurredAtUtc)
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-40 | Domain.Tests | PASS `TicketStatusTests` and `TicketTests` reopen coverage | resolved and closed tickets / reopen each / inspect state + history entry | `In Progress`, with the reopen recorded |
| TC-02 | AC-40 | Api.IntegrationTests | PASS `TicketLifecycleEndpointTests.AC503_Reopening_RecordsReopenedRow_AndSetsStatusToInProgress` (Resolved and Closed) | reopen via API / re-fetch / inspect status + history | persisted, visible in detail read |
| TC-03 | AC-41 | Api.IntegrationTests | PASS `AC41_ConcurrentStatusChange_SecondCallerGets409AndFirstChangeSurvives`, against real SQL Server. Code `TICKET_MODIFIED_BY_ANOTHER_USER`, not `ERR024` | two callers load the same ticket; both change; first saves / second saves / observe | second gets 409 code `ERR024`; first change intact |

## Notes

Two agents resolving the same ticket is ordinary, not exotic, and last-write-wins loses an audit entry — which is the one thing US-121 exists to guarantee. A row version column is cheap by comparison.

## Open questions

None.

## Status evidence

Implemented over `Ticket.ChangeStatus`'s reopen distinction and a client-echoed `rowVersion`.

AC-40 -> `TicketLifecycleEndpointTests.AC503_Reopening_RecordsReopenedRow_AndSetsStatusToInProgress`,
from Resolved and from Closed, each asserting the row is `Reopened` rather than `StatusChanged`. AC-41 ->
`AC41_ConcurrentStatusChange_SecondCallerGets409AndFirstChangeSurvives` and
`AC41_ChangeStatus_WithoutRowVersion_Returns400`, against real SQL Server.

**Design note:** a `rowversion` column alone cannot detect a conflict across two HTTP requests -
each loads the ticket fresh and both succeed. The version travels: the detail read returns it, the
mutation echoes it, and `IRepository.SetOriginalValue` applies it so EF compares against what the
caller actually saw.

**Divergence from AC-66:** the code is `TICKET_MODIFIED_BY_ANOTHER_USER`, not `ERR024`.

Run 2026-08-26: 233 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
