# US-610 — Report Filters

## Problem
Report screens cannot consistently filter by date, category, priority, and branch.

## Assumptions
- A1: Server validates every filter; client controls are convenience only.
- A2: Applied filters are represented in URL/query state.

## Out of scope
Ad-hoc saved reports.

## Acceptance Criteria
- AC-610.1: Date range filters report results and rejects reversed ranges.
- AC-610.2: Category filters report results.
- AC-610.3: Priority filters report results.
- AC-610.4: Branch filter follows US-306/OQ-5 scope rules.

## Design
Create one typed filter DTO, validator, predicate builder, and reusable Angular filter component. Original story: `US-610-report-filter-ui.md` / AC-610.
