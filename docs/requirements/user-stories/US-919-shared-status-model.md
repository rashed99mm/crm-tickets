# US-919 · Shared Frontend Status Model

| Field | Value |
|---|---|
| **Story** | `US-919` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-28`](../delivery-plan.md#feat-28) |
| **Layer** | Frontend |
| **Ships with** | [US-901](./US-901-real-life-8-state-lifecycle.md) *(Backend)* |
| **Actor** | — |
| **Priority** | P0 |
| **Sprint** | 17 — Phase 2 workflow |
| **Estimate** | 3 points |
| **Status** | `done` |

## Story

**As a developer**, **I want** the eight statuses in one shared source of truth, **so that** no
scattered string literal or stale transition table drifts from the backend machine.

## Business rules

- `TICKET_STATUSES`, tints, and `PERMITTED_TRANSITIONS` live together in the shared tickets library
  matching the new machine; every pill/select reads from there.
- `Escalated` is never a status option.

## Acceptance criteria

#### AC1 — Single source of truth

Given the redesign, then a shared status model lists exactly the eight statuses with tints and the
new transition table, and all screens consume it.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Unit | `ticket.api.spec.ts / StatusModel_TICKET_STATUSES_HasEightStatuses` | no `Pending`, no `Escalated` |
| TC-02 | AC1 | Unit | `ticket.api.spec.ts / StatusModel_PERMITTED_TRANSITIONS_Has12LegalPairs` | frontend table matches the backend machine |

## SQL tables

None.

## Notes

Consolidates the scattered status lists, tint maps and transition table into one shared model consumed
by every pill, select and action button.

## Status evidence

Implemented in `frontend/projects/common/src/lib/tickets/status.model.ts` and consumed by the
admin ticket detail status picker. `Escalated` remains an escalation marker, never a status.

Status is set from what is committed and executed, never from what is planned.
