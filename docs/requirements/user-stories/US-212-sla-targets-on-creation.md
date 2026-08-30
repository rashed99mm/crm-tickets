# US-212 · Compute SLA Targets on Ticket Creation

| Field | Value |
|---|---|
| **Story** | `US-212` |
| **Epic** | [EPIC-05 SLA & Escalation](../epics/EPIC-05.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Backend |
| **Ships with** | [US-222](./US-222-sla-frontend-dashboard.md) *(Frontend)* |
| **Actor** | System |
| **Priority** | P0 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-5.2, BR-13 |
| **Spec criteria** | AC-5.2 |
| **Depends on** | [US-210](./US-210-sla-policy-entity.md), [US-215](./EPIC-05-US-215-business-hours-calendar.md) |

## Story

**As a system**, **I want** to compute SLA due dates when a ticket is created, **so that** commitments are tracked.

## Business rules

- BR-13 — ResponseDueAt and ResolutionDueAt are calculated from CreatedAt using matching SLAPolicy targets and branch business-hours calendar (BRD).

## Acceptance criteria

#### AC1 — Compute SLA Targets on Creation (spec AC-5.2)

Given a ticket is created with a priority, when an SLAPolicy matches the ticket's priority (and optional category/branch), then ResponseDueAt and ResolutionDueAt are computed and stored on the Ticket entity.

## SQL tables

`Tickets` — SLA due columns added to existing Tickets table:

```sql
ALTER TABLE [dbo].[Tickets] ADD
    [ResponseDueAt]     DATETIME2    NULL,
    [ResolutionDueAt]   DATETIME2    NULL,
    [EscalationState]   NVARCHAR(50) NULL;
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5.2 | Unit | `CreateTicket_ShouldComputeSLATargets` | Given ticket with High priority and matching SLAPolicy, when ticket is created, then ResponseDueAt and ResolutionDueAt are computed | ResponseDueAt = CreatedAt + policy.ResponseTargetHours; ResolutionDueAt = CreatedAt + policy.ResolutionTargetHours |
| TC-02 | AC-5.2 | Unit | `CreateTicket_NoMatchingPolicy_ShouldNotSetDueDates` | Given ticket with no matching SLAPolicy, when ticket is created, then ResponseDueAt and ResolutionDueAt remain null | Due dates are null |
| TC-03 | AC-5.2 | Integration | `CreateTicket_WithBranchCalendar_ShouldExcludeNonWorkingHours` | Given ticket with branch calendar, when SLA targets are computed, then duration excludes non-working hours | Due dates account for business hours only |

## Notes

Business hours exclusion (US-215) modifies the duration calculation. Without a calendar, targets are computed using wall-clock hours.

## Open questions

None.

## Status evidence

Shipped `FEAT-17` first slice — `Ticket.ResponseDueAt`/`ResolutionDueAt` computed at creation
against the most specific matching active policy; wall-clock hours only (no business-hours
calendar, `US-215`). See `docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-tracking/README.md`.

Status is set from what is committed and executed, never from what is planned.
