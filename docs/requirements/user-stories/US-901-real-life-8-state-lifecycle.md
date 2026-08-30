# US-901 · Real-life 8-state Ticket Lifecycle

| Field | Value |
|---|---|
| **Story** | `US-901` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-28`](../delivery-plan.md#feat-28) |
| **Layer** | Backend / Frontend |
| **Ships with** | [US-919](./US-919-shared-status-model.md) *(Frontend)* |
| **Actor** | Agent, Supervisor |
| **Priority** | P0 |
| **Sprint** | 17 — Phase 2 workflow |
| **Estimate** | 8 points |
| **Status** | `done` |

## Story

**As an agent**, **I want** the ticket to move through the real stages `New → Open → Assigned → In
Progress → (Waiting for Customer / Waiting for Internal Team) → Resolved → Closed`, **so that**
everyone reading a ticket sees exactly how far the work has actually progressed and what is blocking it.

## Business rules

- The lifecycle uses exactly eight statuses; only the transition table defines legal moves; any
  other move (including a no-op to the current status) is refused.
- Reopen lands on `In Progress`, recorded as `Reopened` in history.
- `Pending` is rejected as an unknown status; the two explicit waiting statuses are
  `Waiting for Customer` and `Waiting for Internal Team`.
- `Escalated` remains a marker (`EscalationState`), not a status.

## Acceptance criteria

#### AC1 — Legal transitions applied

Given a ticket in any of the eight statuses, when a listed transition is attempted, then `ChangeStatus`
applies it, history records `StatusChanged` (`Reopened` for a reopen), and a `TicketStatusChangedEvent`
fires.

#### AC2 — Illegal transitions refused

Given a ticket in any status, when a transition not in the table (including a no-op) is attempted,
then the status-change is refused, the ticket does not change, and the refusal surfaces as a 409.

#### AC3 — Reopen to In Progress

Given a ticket in `Resolved` or `Closed`, when reopened, then it lands on `In Progress`, history shows
`Reopened` with the prior status as `FromValue`, and the resolution timestamps are cleared.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Unit | `TicketStatus_AllowsEachLegalTransition` | every legal row transitions, history + event raised |
| TC-02 | AC2 | Unit | `TicketStatus_RefusesEveryIllegalTransition` | refusal raised, status unchanged |
| TC-03 | AC3 | Integration | `Ticket_Reopen_LandsOnInProgress_AndClearsTimestamps` | status `In Progress`, history `Reopened`, timestamps null |

## SQL tables

No schema change — status persists as a string. Existing rows use the eight current string values;
there is no `Pending` status migration in the current model.

## Notes

Migrates `TicketStatus`, `Ticket.ChangeStatus`, `ApplySlaPauseTransition`, the lifecycle
`InvalidOperationException` tests, and the frontend status model/pills (US-919).

## Status evidence

Implemented in `TicketStatus`, `Ticket.ChangeStatus`, the status command handler, and the shared
frontend status model. Domain coverage is `TicketStatusTests`; API coverage is
`TicketLifecycleEndpointTests`; frontend coverage is `ticket.api.spec.ts` and ticket-detail specs.

Status is set from what is committed and executed, never from what is planned.
