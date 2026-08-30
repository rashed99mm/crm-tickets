# US-016 · Move a ticket along its lifecycle

| Field | Value |
|---|---|
| **Story** | `US-016` *(was `US-1.25`)* — rule proposal: *Change Ticket Status* |
| **Epic** | [EPIC-02 Ticket management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-06` Ticket detail and lifecycle](../delivery-plan.md#feat-06--ticket-detail-and-lifecycle) |
| **Layer** | Backend |
| **Ships with** | [US-128](./US-128-ticket-detail-with-guarded-actions.md) *(frontend)* |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [3 — Ticket detail, lifecycle, assignment and history](../delivery-plan.md#sprint-3--ticket-detail-lifecycle-assignment-and-history) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-2.8, BR-3 |
| **Spec criteria** | AC-37 |
| **Depends on** | [US-009](./US-009-raise-a-ticket.md) *(sprint 2)* |

## Story

**As an agent**, **I want** to advance a ticket's status as I work it, **so that** its state reflects reality.

## Business rules

- BR-3 — status changes only along the permitted transition table; other transitions refused as a
  state conflict (409), not a validation error (BRD).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Permitted transition persists (spec AC-37)

Given a permitted transition, then 200 and the new status persists.

## SQL tables

`Tickets.Status` — from the [S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#tickets):

```sql
[Status] NVARCHAR(16) NOT NULL   -- string-persisted enum, mutated only by Ticket.ChangeStatus
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-37 | Domain.Tests | PASS `TicketStatusTests.TicketStatus_AllowsEachLegalTransition` (12 cases) + `TicketTests` for the history append | each legal transition / `Ticket.ChangeStatus` / inspect | state changes and a history entry is appended |
| TC-02 | AC-37 | Api.IntegrationTests | PASS `TicketLifecycleEndpointTests.AC501_ChangeStatus_8StateMachine_PermittedTransition_Returns200` (8 cases, each re-fetching) | a permitted transition via `POST /api/Tickets/{id}/status` / re-fetch | 200; new status persisted on re-fetch |

## Notes

Permitted: `New → Open` · `Open → Assigned` · `Open → Resolved` · `Assigned → In Progress` ·
`In Progress → Waiting for Customer` · `In Progress → Waiting for Internal Team` ·
`In Progress → Resolved` · `Waiting for Customer → In Progress` ·
`Waiting for Internal Team → In Progress` · `Resolved → In Progress` ·
`Resolved → Closed` · `Closed → In Progress`.

The transition table lives in `TicketStatus`, consulted by `Ticket.ChangeStatus`. Work states require
an assignee; the status setter is private. `Pending` and `Escalated` are not valid status values.

## Open questions

None.

## Status evidence

Implemented as `ChangeTicketStatusCommand` and `POST /api/Tickets/{id}/status`.

AC-37 -> `TicketLifecycleEndpointTests.AC501_ChangeStatus_8StateMachine_PermittedTransition_Returns200`,
with eight API cases, while the domain theory covers all twelve legal pairs.
re-fetching to prove persistence rather than trusting the 200. The eight-edge domain table is
covered separately by `TicketStatusTests` from Phase 0.

A sub-resource POST rather than a PATCH: a status change is a transition, not a field assignment.

Run 2026-08-26: 233 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
