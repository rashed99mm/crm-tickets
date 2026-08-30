# US-610 · Report Filter Controls

| Field | Value |
|---|---|
| **Story** | `US-610` |
| **Epic** | [EPIC-08 Reports & Management](../epics/EPIC-08-reporting.md) |
| **Feature** | [`FEAT-19` Reporting](../delivery-plan.md#feat-19--reporting) |
| **Layer** | Frontend |
| **Ships with** | [US-601](./US-601-reports-controller.md) *(Backend)* |
| **Actor** | Manager |
| **Priority** | P1 |
| **Sprint** | [13 — Reporting](../delivery-plan.md#sprint-13-reporting) · Slice S6 |
| **Estimate** | 5 points |
| **Status** | `partial` |
| **BRD requirements** | FR-9.1 |
| **Spec criteria** | AC-610 |
| **Depends on** | [US-601](./US-601-reports-controller.md) *(Backend)*, [US-608](./EPIC-08-US-608-report-scoping.md) *(Backend)* |

## Story

**As a manager**, **I want** to filter reports by date range, category, priority, and branch, **so that** I can drill into the specific data I need.

## Business rules

- No BRD BR-n covers this directly. Report filters include date range (from/to), category, priority, and branch.
- No BRD BR-n covers this directly. Changing any filter triggers a new API request with updated query parameters.
- BR-21: Branch filter is enforced via JWT claims; users cannot override their own branch scope.

## Acceptance criteria

#### AC1 — Date range filter (spec AC-610)

Given the manager is on a report page, when they set a from/to date range and apply, then the report data refreshes for that period.

#### AC2 — Category filter (spec AC-610)

Given the manager is on a report page, when they select one or more categories, then only tickets in those categories are included.

#### AC3 — Priority filter (spec AC-610)

Given the manager is on a report page, when they select one or more priorities, then only tickets with those priorities are included.

#### AC4 — Branch filter (spec AC-610)

Given the manager is on a report page, when they select a branch, then only tickets from that branch are included.

## SQL tables

None — read-only query over existing tables.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-610 | Component | `ReportFilterDateRange` | Given the manager sets from=2026-01-01 and to=2026-01-31, when they apply the filter, then the HTTP request includes both date params. | Correct date parameters sent |
| TC-02 | AC-610 | Component | `ReportFilterCategory` | Given the manager selects "Billing" category, when they apply the filter, then only billing data is displayed. | Category filter applied |
| TC-03 | AC-610 | Component | `ReportFilterPriority` | Given the manager selects "High" priority, when they apply the filter, then only high-priority data is displayed. | Priority filter applied |

## Notes

Frontend component uses Angular reactive forms with signals. Filter state is reflected in URL query parameters for shareability. Uses mockup `dashboard-overview.html` as reference for filter layout.

## Open questions

None.

## Status evidence

**Narrowed, not built as specced** (spec addendum A4): only AC1 (date range) shipped —
`ReportDateRangeFilter` (`common`), reused by all three report screens, syncing to the url's query
params. AC2–AC4 (category/priority/branch filters) not built: category and priority are breakdown
*dimensions* the ticket-volume report already returns, not filters layered on top of it, and
branch filtering doesn't exist (`US-608`). `npx ng test common --watch=false
--include='**/report-date-range-filter.component.spec.ts'` → 1/1 passing. See
`docs/superpowers/plans/EPIC-08-US-606-feat-reporting/README.md`. **Not yet committed** — staged only,
per explicit instruction this session.

Status is set from what is committed and executed, never from what is planned.
