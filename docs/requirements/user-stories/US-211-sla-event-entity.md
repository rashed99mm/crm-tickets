# US-211 · SLA Event Entity

| Field | Value |
|---|---|
| **Story** | `US-211` |
| **Epic** | [EPIC-05 SLA & Escalation](../epics/EPIC-05.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Backend |
| **Ships with** | [US-210](./US-210-sla-policy-entity.md) *(Backend)* |
| **Actor** | System |
| **Priority** | P0 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 2 points |
| **Status** | `done` |
| **BRD requirements** | FR-5.5, BR-15 |
| **Spec criteria** | AC-5.5 |
| **Depends on** | [US-201](./US-201-ticket-entity.md) |

## Story

**As a system**, **I want** to record SLA events, **so that** breach history is maintained.

## Business rules

- BR-15 — SLA events are immutable records of target tracking state changes (BRD).

## Acceptance criteria

#### AC1 — Record SLA Event (spec AC-5.5)

Given an SLA target is set on a ticket, when a breach is detected, then an SLAEvent is recorded with TicketId, TargetType, TargetAt, BreachedAt, and PausedSeconds.

## SQL tables

`SLAEvents` — immutable log of SLA tracking events per ticket:

```sql
CREATE TABLE [dbo].[SLAEvents] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [TicketId]        UNIQUEIDENTIFIER NOT NULL,
    [TargetType]      NVARCHAR(50)     NOT NULL,
    [TargetAt]        DATETIME2        NOT NULL,
    [BreachedAt]      DATETIME2        NULL,
    [PausedSeconds]   INT              NOT NULL DEFAULT 0,
    [CreatedAt]       DATETIME2        NOT NULL,
    CONSTRAINT [PK_SLAEvents] PRIMARY KEY ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5.5 | Unit | `RecordSLAEvent_ShouldStoreEvent` | Given ticket with SLA target, when breach is detected, then SLAEvent is recorded | Event persisted with correct TicketId, TargetType, TargetAt, BreachedAt, PausedSeconds |

## Notes

SLAEvents will be written by the breach detection background job (US-216) and the pause/resume logic (US-213). The BreachedAt is null until a breach occurs.

## Open questions

None.

## Status evidence

Shipped `FEAT-17` first slice — `SLAEvent` entity (`IAppendOnlyEntity`). See
`docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-tracking/README.md`.

Status is set from what is committed and executed, never from what is planned.
