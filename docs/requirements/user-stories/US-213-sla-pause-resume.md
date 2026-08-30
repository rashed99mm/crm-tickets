# US-213 · SLA Clock Pause/Resume on Waiting States

| Field | Value |
|---|---|
| **Story** | `US-213` |
| **Epic** | [EPIC-05 SLA & Escalation](../epics/EPIC-05.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Backend |
| **Ships with** | [US-222](./US-222-sla-frontend-dashboard.md) *(Frontend)* |
| **Actor** | System |
| **Priority** | P1 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-5.3, BR-16, BR-17 |
| **Spec criteria** | AC-5.3 |
| **Depends on** | [US-212](./US-212-sla-targets-on-creation.md) |

## Story

**As a system**, **I want** to pause SLA when waiting on customer, **so that** resolution time is accurate.

## Business rules

- BR-16 — SLA clock pauses when ticket status is `Waiting for Customer` or `Waiting for Internal Team`
  and resumes when the ticket exits either waiting state (BRD).
- BR-17 — SLA resumes on exit from either waiting state (BRD).

## Acceptance criteria

#### AC1 — Pause SLA on Waiting States (spec AC-5.3)

Given a ticket with an active SLA clock, when the ticket transitions to `Waiting for Customer` or
`Waiting for Internal Team`, then the SLA clock pauses and accumulated paused time is tracked.

#### AC2 — Resume SLA on Status Change (spec AC-5.3)

Given a ticket in either waiting status with paused SLA, when the ticket transitions to `In Progress`,
then the SLA clock resumes and due dates are adjusted by the paused duration.

## SQL tables

`Tickets` — SLA tracking columns on Tickets table:

```sql
ALTER TABLE [dbo].[Tickets] ADD
    [TotalPausedSeconds] INT          NOT NULL DEFAULT 0,
    [PausedAt]           DATETIME2    NULL;
```

`SLAEvents` — records pause/resume transitions (defined in [US-211](./US-211-sla-event-entity.md)):

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
| TC-01 | AC-5.3 | Unit | `Ticket_SlaPause_WaitingForCustomer_ShiftsDues` | Given ticket with active SLA, when status transitions to Waiting for Customer, then PausedAt is set and clock is paused | PausedAt is current time; SLA not counting |
| TC-02 | AC-5.3 | Unit | `Ticket_SlaPause_WaitingForInternal_ShiftsDues` | Given ticket with active SLA, when status transitions to Waiting for Internal Team, then PausedAt is set and clock is paused | PausedAt is current time; SLA not counting |
| TC-03 | AC-5.3 | Unit | `AC136_MultipleWaitingCycles_AccumulatePausedSeconds` | Given ticket with multiple waiting transitions, when each cycle completes, then total pause time accumulates correctly | TotalPausedSeconds is sum of all pause durations |

## Notes

Due date adjustment must account for business hours if a calendar is configured. The pause/resume events are also written to SLAEvents for audit.

## Open questions

None.

## Status evidence

Shipped `FEAT-17` second slice — `Ticket.PausedAt`/`TotalPausedSeconds`; entering either waiting
status starts the pause, leaving it accumulates elapsed time and shifts both due dates forward by
the same span.
Fixed a real correctness gap the first slice's approximation had (a ticket paused for days would
otherwise have been flagged breached the moment it returned to `Open`). Known gap:
`TotalPausedSeconds` truncates sub-second spans to zero. See
`docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-escalation/README.md`.

Status is set from what is committed and executed, never from what is planned.
