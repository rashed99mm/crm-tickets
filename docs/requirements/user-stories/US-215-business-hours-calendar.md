# US-215 · Branch Business-Hours Calendar

| Field | Value |
|---|---|
| **Story** | `US-215` |
| **Epic** | [EPIC-05 SLA & Escalation](../epics/EPIC-05.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Backend |
| **Ships with** | [US-212](./US-212-sla-targets-on-creation.md) *(Backend)* |
| **Actor** | System / Admin |
| **Priority** | P1 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-5.4, BR-14, BR-17 |
| **Spec criteria** | AC-5.4 |
| **Depends on** | [US-201](./US-201-ticket-entity.md) |

## Story

**As a system**, **I want** to maintain business-hours calendars per branch, **so that** SLA durations exclude non-working time.

## Business rules

- BR-14 — SLA duration calculation must only count hours within the configured business-hours calendar for the branch (BRD).
- BR-17 — Public holidays are excluded from SLA duration calculations (BRD).

## Acceptance criteria

#### AC1 — Business Hours Calendar Configuration (spec AC-5.4)

Given a branch calendar is configured with working hours and holidays, when an SLA duration is calculated, then only working hours are counted and public holidays are excluded.

## SQL tables

`BusinessHoursCalendars` — per-branch working hours configuration:

```sql
CREATE TABLE [dbo].[BusinessHoursCalendars] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [BranchId]        UNIQUEIDENTIFIER NOT NULL,
    [DayOfWeek]       INT              NOT NULL,
    [OpenTime]        TIME             NOT NULL,
    [CloseTime]       TIME             NOT NULL,
    [CreatedAt]       DATETIME2        NOT NULL,
    [UpdatedAt]       DATETIME2        NOT NULL,
    CONSTRAINT [PK_BusinessHoursCalendars] PRIMARY KEY ([Id])
);
```

`PublicHolidays` — holiday exclusions:

```sql
CREATE TABLE [dbo].[PublicHolidays] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [BranchId]        UNIQUEIDENTIFIER NOT NULL,
    [HolidayDate]     DATE             NOT NULL,
    [Name]            NVARCHAR(200)    NOT NULL,
    [CreatedAt]       DATETIME2        NOT NULL,
    CONSTRAINT [PK_PublicHolidays] PRIMARY KEY ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5.4 | Unit | `CalculateDuration_ShouldExcludeWeekends` | Given working hours Mon–Fri 9–5, when calculating 48 hours from Friday 4pm, then result skips weekend | Due date is Wednesday 4pm (not Monday 4pm) |
| TC-02 | AC-5.4 | Unit | `CalculateDuration_ShouldExcludeHolidays` | Given public holiday on Tuesday, when calculating across that day, then holiday is excluded | Duration skips Tuesday entirely |
| TC-03 | AC-5.4 | Unit | `CalculateDuration_ShouldRespectWorkingHours` | Given working hours 9–5, when calculating 8 hours from 3pm, then result spans into next day | Due date is next working day 1pm |

## Notes

The calendar is per-branch. If no calendar exists for a branch, wall-clock hours are used as fallback. Calendar data is managed via admin CRUD (not a separate user story, as it falls under branch management).

## Open questions

None.

## Status evidence

**Explicitly cut, not built** — `FEAT-17`'s spec assumption A1: wall-clock hours only, no
business-hours calendar. See `docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-tracking/README.md`.

Status is set from what is committed and executed, never from what is planned.
