# US-609 — Report Export

## Problem
Users cannot export authorized report results for offline analysis.

## Assumptions
- A1: Export uses exactly the same filters and scope as the visible report.
- A2: Exports are bounded and streamed.

## Out of scope
Scheduled exports and arbitrary SQL/report builders.

## Acceptance Criteria
- AC-609.1: CSV export contains the selected report data.
- AC-609.2: Excel export contains the selected report data.
- AC-609.3: Export cannot bypass report scoping.

## Design
Implement format writers behind one export endpoint and add authorized UI actions. Original story: `US-609-export-report.md` / AC-609.
