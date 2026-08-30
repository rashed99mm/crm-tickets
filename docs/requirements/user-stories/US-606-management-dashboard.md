# US-606 · Management Overview Dashboard

| Field | Value |
|---|---|
| **Story** | `US-606` |
| **Epic** | [EPIC-08 Reports & Management](../epics/EPIC-08-reporting.md) |
| **Feature** | [`FEAT-19` Reporting](../delivery-plan.md#feat-19--reporting) |
| **Layer** | Backend / Frontend |
| **Ships with** | [US-601](./US-601-reports-controller.md) *(Backend)*, [US-610](./US-610-report-filter-ui.md) *(Frontend)* |
| **Actor** | Manager |
| **Priority** | P1 |
| **Sprint** | [13 — Reporting](../delivery-plan.md#sprint-13-reporting) · Slice S6 |
| **Estimate** | 8 points |
| **Status** | `not started` |
| **BRD requirements** | FR-9.5 |
| **Spec criteria** | AC-606, DSH-1 |
| **Depends on** | [US-601](./US-601-reports-controller.md) *(Backend)*, [US-610](./US-610-report-filter-ui.md) *(Frontend)* |

## Story

**As a manager**, **I want** a management overview dashboard, **so that** I can see the current state of the support operation at a glance.

## Business rules

- No BRD BR-n covers this directly. The management dashboard aggregates open ticket count, average wait time, SLA attainment, and CSAT score into a single view.
- BR-21: Report results are branch-scoped by default; the caller's branch is enforced via JWT claims.

## Acceptance criteria

#### AC1 — Dashboard summary cards (spec AC-606)

Given the system has current ticket data, when the manager opens the dashboard, then summary cards show open tickets, average wait, SLA %, and CSAT average.

#### AC2 — Dashboard API endpoint (spec AC-606)

Given the manager is authenticated, when the dashboard loads, then a single `/api/reports/dashboard` endpoint returns all summary metrics.

## SQL tables

None — read-only query over existing tables.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-606 | Integration | `DashboardSummaryEndpoint` | Given tickets exist, when the manager calls `GET /api/reports/dashboard`, then a 200 response with all four summary metrics is returned. | 200 OK with openTickets, avgWait, slaAttainment, avgCsat |
| TC-02 | DSH-1 | E2E | `DashboardRendersSummaryCards` | Given the manager is logged in, when they navigate to the dashboard, then four summary cards are visible with live data. | Cards display non-null numeric values |

## Notes

Backend: `ReportsController` exposes the `/api/reports/dashboard` endpoint. Frontend: `DashboardComponent` renders the overview. Uses mockup `dashboard-overview.html`.

## Open questions

None.

## Status evidence

**Deliberately not built as specced** (spec addendum A4, `EPIC-08-US-606-reporting.md`): needs
a `/api/reports/dashboard` endpoint, CSAT data (no rating-collection mechanism exists anywhere in
this codebase — `US-605`), and branch scoping (`US-608`, not built). Rather than build that
additional backend, the equivalent-but-narrower `US-602`/`US-603`/`US-604` report screens shipped
instead, against the endpoints that actually exist. This story remains open against its literal
acceptance criteria.

Status is set from what is committed and executed, never from what is planned.
