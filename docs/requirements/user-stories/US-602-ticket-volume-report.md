# US-602 · Ticket Volume Report

| Field | Value |
|---|---|
| **Story** | `US-602` |
| **Epic** | [EPIC-08 Reports & Management](../epics/EPIC-08-reporting.md) |
| **Feature** | [`FEAT-19` Reporting](../delivery-plan.md#feat-19--reporting) |
| **Layer** | Backend |
| **Ships with** | [US-601](./US-601-reports-controller.md) *(Backend)* |
| **Actor** | Manager |
| **Priority** | P0 |
| **Sprint** | [13 — Reporting](../delivery-plan.md#sprint-13-reporting) · Slice S6 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-9.1 |
| **Spec criteria** | AC-602 |
| **Depends on** | [US-601](./US-601-reports-controller.md) *(Backend)*, [US-608](./EPIC-08-US-608-report-scoping.md) *(Backend)* |

## Story

**As a manager**, **I want** to retrieve ticket volume broken down by time period, category, and priority, **so that** I understand the team's workload distribution.

## Business rules

- No BRD BR-n covers this directly. Ticket volume report groups by period (daily/weekly/monthly), category, and priority.
- BR-21: Report results are branch-scoped by default; the caller's branch is enforced via JWT claims.

## Acceptance criteria

#### AC1 — Volume by period (spec AC-602)

Given tickets exist across multiple dates, when the manager requests the volume report for a date range, then the response contains ticket counts grouped by day/week/month.

#### AC2 — Volume by category (spec AC-602)

Given tickets exist with various categories, when the manager requests the volume report, then category breakdowns are included.

#### AC3 — Volume by priority (spec AC-602)

Given tickets exist with various priorities, when the manager requests the volume report, then priority breakdowns are included.

## SQL tables

None — read-only query over existing tables.

```sql
SELECT DATE(createdAt) AS period, category, priority, COUNT(*) AS ticketCount
FROM Tickets
WHERE createdAt BETWEEN @startDate AND @endDate
GROUP BY DATE(createdAt), category, priority;
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-602 | Integration | `TicketVolumeReportByPeriod` | Given 10 tickets created this week, when the manager requests weekly volume, then the response shows 10 tickets for this week. | Correct period grouping |
| TC-02 | AC-602 | Integration | `TicketVolumeReportByCategory` | Given 5 billing and 5 tech tickets, when the manager requests volume, then billing=5 and tech=5 appear. | Correct category breakdown |
| TC-03 | AC-602 | Integration | `TicketVolumeReportByPriority` | Given 7 high and 3 low priority tickets, when the manager requests volume, then high=7 and low=3 appear. | Correct priority breakdown |

## Notes

Supports `from`, `to`, `groupBy` (day|week|month) query parameters. Returns structured JSON with nested breakdowns.

## Open questions

None.

## Status evidence

Backend shipped: `GetTicketVolumeReportQuery`, AC-149/150/151 tested in `ReportsEndpointTests.cs`
(3/3 passing). Frontend shipped 2026-08-27: `TicketVolumeReportComponent` (AC-160), 1 of 4 tests in
`ticket-volume-report.component.spec.ts` covering this story directly. See
`docs/superpowers/plans/EPIC-08-US-606-feat-reporting/README.md` for full evidence including the
"Frontend addendum" section. Frontend **not yet committed** — staged only, per explicit
instruction this session.

Status is set from what is committed and executed, never from what is planned.
