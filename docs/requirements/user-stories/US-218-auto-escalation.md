# US-218 · Auto-Escalate on Threshold

| Field | Value |
|---|---|
| **Story** | `US-218` |
| **Epic** | [EPIC-05 SLA & Escalation](../epics/EPIC-05-sla-and-automation.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Backend |
| **Ships with** | [US-225](./US-225-ticket-escalation-state.md) *(backend)* |
| **Actor** | System |
| **Priority** | P0 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-5.7 |
| **Spec criteria** | AC-5.7 |
| **Depends on** | [US-216](./US-216-sla-breach-detection.md), [US-225](./US-225-ticket-escalation-state.md) |

## Story

**As a system**, **I want** to auto-escalate tickets when SLA thresholds are crossed, **so that** at-risk tickets receive timely attention from the appropriate escalation level.

## Business rules

- BR-21 — Ticket escalation state transitions are driven by SLA breach events (BRD).
- BR-10 — Supervisor assigns and reassigns tickets; escalation may route to supervisor level (BRD).

## Acceptance criteria

#### AC1 — Auto-Escalate on Breach (spec AC-5.7)

Given a ticket with an active SLA target, when the SLA breach is detected and confirmed, then the ticket's `EscalationState` is updated to the next escalation level and a notification is sent to the role defined at that level.

#### AC2 — Escalation Progresses Through Levels (spec AC-5.7)

Given a ticket already at escalation level 1, when the next breach threshold is crossed, then the ticket escalates to level 2 and the corresponding target role is notified.

## SQL tables

`EscalationLevels` — configurable escalation level definitions:

```sql
CREATE TABLE [dbo].[EscalationLevels] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [Name]            NVARCHAR(100)    NOT NULL,
    [Level]           INT              NOT NULL,
    [TargetRoleId]    UNIQUEIDENTIFIER NOT NULL,
    [BreachMinutes]   INT              NOT NULL,
    [CreatedAt]       DATETIME2        NOT NULL,
    CONSTRAINT [PK_EscalationLevels] PRIMARY KEY ([Id])
);
```

`Tickets` — `EscalationState` column defined in [US-225](./US-225-ticket-escalation-state.md):

```sql
ALTER TABLE [dbo].[Tickets]
    ADD [EscalationState] NVARCHAR(50) NULL;
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5.7 | Unit | `BreachDetection_ShouldEscalateTicket` | Given a ticket with an active SLA that breaches, when the breach is confirmed by the monitoring job, then the ticket's `EscalationState` is updated to "Level1" | `EscalationState` = `"Level1"` |
| TC-02 | AC-5.7 | Unit | `Escalation_ShouldNotifyTargetRole` | Given a ticket escalated to level 1, when the escalation is processed, then a notification is sent to the role configured for level 1 | Notification delivered to supervisor role |
| TC-03 | AC-5.7 | Unit | `Escalation_ShouldProgressLevels` | Given a ticket already at escalation level 1, when the next breach threshold is crossed, then the ticket escalates to level 2 | `EscalationState` updated to `"Level2"` |

## Notes

Escalation state is defined in [US-225](./US-225-ticket-escalation-state.md). This story implements the background job logic that detects SLA breaches and triggers state transitions. Escalation levels are configurable via `EscalationLevels` and determine which role is notified at each level.

## Open questions

None.

## Status evidence

Implemented in `SlaBreachScanner`, `EscalationLevelProvider`, and `EscalationLevelSeeder`: each new
breach advances to the next active configured level, publishes `SlaEscalatedMessage`, and notifies
the level's `TargetRole` in addition to the assignee and Supervisor role. The seeded Level1 to
Level2 ladder is idempotent and terminal when no higher level exists.

Status is set from what is committed and executed, never from what is planned.
