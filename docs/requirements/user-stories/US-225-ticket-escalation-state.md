# US-225 · Add Escalation State to Ticket Entity

| Field | Value |
|---|---|
| **Story** | `US-225` |
| **Epic** | [EPIC-02 Ticket Management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Backend |
| **Ships with** | [US-218](./EPIC-05-US-218-auto-escalation.md), [US-224](./US-224-escalation-badge.md) *(backend)* |
| **Actor** | System |
| **Priority** | P0 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 2 points |
| **Status** | `done` |
| **BRD requirements** | FR-2.14 |
| **Spec criteria** | AC-2.14 |
| **Depends on** | [US-201](./US-201-notification-service.md) |

## Story

**As a system**, **I want** an escalation state field on the ticket entity, **so that** escalation progress is tracked and visible across the application.

## Business rules

- BR-31 — `EscalationState` is a nullable enum on the Ticket entity (BRD).
- BR-32 — Valid escalation states: `None`, `Warning`, `Level1`, `Level2`, `Level3` (BRD).

## Acceptance criteria

#### AC1 — Ticket Entity Escalation State (spec AC-2.14)

Given the Ticket entity is defined, when the entity schema is inspected, then it includes a nullable `EscalationState` field with valid values `None`, `Warning`, `Level1`, `Level2`, `Level3`.

#### AC2 — Escalation State Transitions (spec AC-2.14)

Given a ticket with escalation state `None`, when the state changes via SLA breach detection, then the new state is persisted to the database and an audit entry is created.

## SQL tables

`Tickets` — `EscalationState` column added to the Tickets table:

```sql
ALTER TABLE [dbo].[Tickets]
    ADD [EscalationState] NVARCHAR(50) NULL;
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-2.14 | Unit | `TicketEntity_ShouldHaveEscalationState` | Given the ticket entity is defined, when inspected, then the `EscalationState` property exists as a nullable enum | Property present, nullable enum |
| TC-02 | AC-2.14 | Unit | `TicketEntity_DefaultEscalationState_ShouldBeNone` | Given a new ticket is created, when the entity is initialised, then `EscalationState` defaults to `None` | Default value is `None` |
| TC-03 | AC-2.14 | Integration | `EscalationStateChange_ShouldPersist` | Given a ticket with state `None`, when escalated to `Level1`, then the new state is persisted to the database | DB reflects `Level1` |

## Notes

This entity change is shared with EPIC-02 (Ticket Management) as tickets are the core entity. The escalation state is consumed by the frontend ([US-224](./US-224-escalation-badge.md)) and updated by the auto-escalation background job ([US-218](./EPIC-05-US-218-auto-escalation.md)).

## Open questions

None.

## Status evidence

Shipped `FEAT-17` second slice — `Ticket.EscalationState` column (`None`/`Warning`/`Level1`/
`Level2`/`Level3` per `BR-32`; only `None`/`Level1` reachable this slice), exposed via
`TicketDetailDto` and, since 2026-08-27, `TicketListItemDto` (`US-224`). See
`docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-escalation/README.md`.

Status is set from what is committed and executed, never from what is planned.
