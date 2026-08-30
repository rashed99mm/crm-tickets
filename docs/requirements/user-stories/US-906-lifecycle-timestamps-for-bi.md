# US-906 · Lifecycle Timestamps for BI

| Field | Value |
|---|---|
| **Story** | `US-906` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-28`](../delivery-plan.md#feat-28) |
| **Layer** | Backend |
| **Ships with** | [US-909](./US-909-report-accuracy-improvements.md) *(Backend)* |
| **Actor** | — |
| **Priority** | P0 |
| **Sprint** | 17 — Phase 2 workflow |
| **Estimate** | 4 points |
| **Status** | `not started` |

## Story

**As a manager**, **I want** first-response and handle time measured from real lifecycle timestamps,
**so that** the BI reports stop approximating with `UpdatedAt`.

## Business rules

- `FirstResponseAt`/`LastResponseAt` are stamped when an outbound `TicketMessage` is recorded
  (FEAT-14 handler); `ResolvedAt`/`ClosedAt` on the transitions into those statuses; cleared on
  reopen.
- `agent-performance.avgHandleMinutes` switches to `ResolvedAt - CreatedAt` (paused-adjusted) and
  first-response uses `FirstResponseAt - CreatedAt`.

## Acceptance criteria

#### AC1 — Timestamps stamped

Given a ticket, when the first outbound message is recorded, then `FirstResponseAt` set; when later
messages are recorded, then `LastResponseAt` updates; when it enters `Resolved`/`Closed`, then the
matching timestamp is set; when reopened, then they clear.

#### AC2 — Report accuracy

Given resolved tickets, then `agent-performance.avgHandleMinutes` uses `ResolvedAt`; given tickets
with a first response, then response times use `FirstResponseAt`.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Unit | `Ticket_RecordResponse_SetsFirstAndLast` | first preserves, last overwrites |
| TC-02 | AC1 | Integration | `Ticket_ResolveClosed_Stamp_ClearOnReopen` | stamps set, cleared on reopen |
| TC-03 | AC2 | Integration | `AgentPerformance_TimestampBased` | uses `ResolvedAt`, not `UpdatedAt` |
| TC-04 | AC1 | Integration | `FirstOutboundMessage_StampsTicket` | handler stamps on outbound |

## SQL tables

`Tickets` gains four nullable `DateTime` columns.

## Notes

The stamping happens where outbound messages are recorded (the FEAT-14 message-recording flow).
Ticket DTOs gain the fields.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.