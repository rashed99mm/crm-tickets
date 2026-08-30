# US-603 · SLA Performance Report

| Field | Value |
|---|---|
| **Story** | `US-603` |
| **Epic** | [EPIC-08 Reports & Management](../epics/EPIC-08-reporting.md) |
| **Feature** | [`FEAT-19` Reporting](../delivery-plan.md#feat-19--reporting) |
| **Layer** | Backend |
| **Ships with** | [US-601](./US-601-reports-controller.md) *(Backend)* |
| **Actor** | Manager |
| **Priority** | P0 |
| **Sprint** | [13 — Reporting](../delivery-plan.md#sprint-13-reporting) · Slice S6 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-9.2 |
| **Spec criteria** | AC-603 |
| **Depends on** | [US-601](./US-601-reports-controller.md) *(Backend)*, [US-608](./EPIC-08-US-608-report-scoping.md) *(Backend)* |

## Story

**As a manager**, **I want** SLA attainment and breach data in a report, **so that** I track whether the team meets its commitments.

## Business rules

- No BRD BR-n covers this directly. SLA report includes first-response time and resolution time against configured thresholds per priority.
- BR-21: Report results are branch-scoped by default; the caller's branch is enforced via JWT claims.

## Acceptance criteria

#### AC1 — SLA attainment rate (spec AC-603)

Given tickets with first-response and resolution timestamps, when the manager requests the SLA report, then the response shows percentage of tickets meeting first-response and resolution SLAs.

#### AC2 — SLA breach count (spec AC-603)

Given tickets that breached SLA thresholds, when the manager requests the SLA report, then breaches are counted and grouped by priority.

## SQL tables

None — read-only query over existing tables.

```sql
SELECT priority,
       COUNT(*) AS totalTickets,
       SUM(CASE WHEN firstResponseAt <= firstResponseSla THEN 1 ELSE 0 END) AS metFirstResponse,
       SUM(CASE WHEN resolvedAt <= resolutionSla THEN 1 ELSE 0 END) AS metResolution
FROM Tickets
WHERE createdAt BETWEEN @startDate AND @endDate
GROUP BY priority;
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-603 | Integration | `SlaReportAttainmentRate` | Given 8 of 10 high-priority tickets met first-response SLA, when the manager requests the SLA report, then attainment is 80%. | 80% attainment for high priority |
| TC-02 | AC-603 | Integration | `SlaReportBreachCount` | Given 2 tickets breached resolution SLA, when the manager requests breaches, then breach count is 2. | Breach count matches |

## Notes

SLA thresholds are configured per priority in PlatformSettings. The report compares actual timestamps against thresholds.

## Open questions

None.

## Status evidence

Backend shipped: `GetSlaPerformanceReportQuery`, AC-152 tested (`met + breached = total` invariant
proven per priority). Frontend shipped 2026-08-27: `SlaPerformanceReportComponent` (AC-161). See
`docs/superpowers/plans/EPIC-08-US-606-feat-reporting/README.md`. Frontend **not yet committed** —
staged only, per explicit instruction this session.

Status is set from what is committed and executed, never from what is planned.
