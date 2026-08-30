# US-904 · Escalation Owner + Marker

| Field | Value |
|---|---|
| **Story** | `US-904` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-28`](../delivery-plan.md#feat-28) |
| **Layer** | Backend / Frontend |
| **Ships with** | [US-920](./US-920-escalation-banner-ui.md) *(Frontend)* |
| **Actor** | Supervisor |
| **Priority** | P0 |
| **Sprint** | 17 — Phase 2 workflow |
| **Estimate** | 3 points |
| **Status** | `not started` |

## Story

**As a supervisor**, **I want** an escalated ticket to name who is owning it, **so that** escalation
is a hand-off to a real person, not just a flag.

## Business rules

- `Ticket.EscalationAssigneeId` (nullable) names the Supervisor/Specialist holding an escalated
  ticket. `EscalationState` levels remain the driver; the marker renders from both.
- History records an `Escalated` row on hand-off; a subsequent hand-off records another `Escalated`
  row (append-only).
- The status picker never offers a 9th `Escalated` status.

## Acceptance criteria

#### AC1 — Escalation hand-off names an owner

Given a supervisor/specialist takes ownership of an escalated ticket, then
`EscalationAssigneeId` is set, an `Escalated` history row records the owner change, and the ticket's
`EscalationState` reflects the level.

#### AC2 — Marker, not a status

Given a ticket with `EscalationState != "None"`, then any screen rendering it shows the escalated
marker (level + owner) and the status remains a main-thread status, never an `Escalated` option.

#### AC3 — Escalation banner + hand-off UI

Given a supervisor views an escalated ticket, then a banner shows the level and owner; a hand-off
control moves it to a named Specialist from the detail screen.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Unit | `Ticket_TakeEscalation_SetsOwner_RecordsHistory` | owner set + history row + level |
| TC-02 | AC2 | Unit | `TicketStatus_All_DoesNotIncludeEscalated` | `All` exactly the 8 statuses |
| TC-03 | AC3 | Component | `TicketDetail_EscalationBanner_ShowsOwner` | banner renders level + owner |

## SQL tables

`TicketEscalationAssigneeId` column (new, nullable, FK Restrict to AspNetUsers optional per ADR-0014
system-actor precedent — see FEAT-28 plan).

## Notes

Domain: a `TakeEscalation` operation on `Ticket`. API: extend the assign payload or expose a dedicated
escalation-owner action on the ticket endpoint. Frontend: US-920.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.