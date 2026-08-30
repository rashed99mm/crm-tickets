# US-609 · Export Report to Spreadsheet

| Field | Value |
|---|---|
| **Story** | `US-609` |
| **Epic** | [EPIC-08 Reports & Management](../epics/EPIC-08-reporting.md) |
| **Feature** | [`FEAT-19` Reporting](../delivery-plan.md#feat-19--reporting) |
| **Layer** | Backend / Frontend |
| **Ships with** | [US-601](./US-601-reports-controller.md) *(Backend)*, [US-610](./US-610-report-filter-ui.md) *(Frontend)* |
| **Actor** | Manager |
| **Priority** | P2 |
| **Sprint** | [13 — Reporting](../delivery-plan.md#sprint-13-reporting) · Slice S6 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-9.6 |
| **Spec criteria** | AC-609 |
| **Depends on** | [US-601](./US-601-reports-controller.md) *(Backend)*, [US-610](./US-610-report-filter-ui.md) *(Frontend)* |

## Story

**As a manager**, **I want** to export reports to a spreadsheet, **so that** I can share data offline and include it in presentations.

## Business rules

- No BRD BR-n covers this directly. Reports can be exported as CSV or Excel (.xlsx) format.
- No BRD BR-n covers this directly. Exported data respects the same scoping rules as on-screen reports.
- BR-21: Report results are branch-scoped by default; the caller's branch is enforced via JWT claims.

## Acceptance criteria

#### AC1 — CSV export (spec AC-609)

Given a manager viewing a report, when they click export, then a CSV file downloads with the same data displayed on screen.

#### AC2 — Excel export (spec AC-609)

Given a manager viewing a report, when they select Excel format and click export, then an .xlsx file downloads.

#### AC3 — Export respects scoping (spec AC-609)

Given a manager with department scope, when they export a report, then only their department data appears in the file.

## SQL tables

None — read-only query over existing tables.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-609 | Integration | `ExportReportAsCsv` | Given report data, when the manager exports as CSV, then a valid CSV file with correct headers and data is returned. | CSV file with report data |
| TC-02 | AC-609 | Integration | `ExportReportAsExcel` | Given report data, when the manager exports as Excel, then a valid .xlsx file is returned. | Excel file with report data |
| TC-03 | AC-609 | Integration | `ExportRespectsScope` | Given a manager in Dept A, when they export, then only Dept A rows appear in the file. | Department-scoped export |

## Notes

Backend: `ReportsController` adds a `format` query parameter (csv|xlsx). Frontend: export button on each report view. Uses OpenXML SDK or CsvHelper for file generation.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
