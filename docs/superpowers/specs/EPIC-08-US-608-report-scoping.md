# US-608 — Report Scoping

## Problem
Reports must not expose data across a user's department or branch boundary.

## Assumptions
- A1: Scope is derived from claims/relationships, never trusted from a query parameter.
- A2: Administrators have an explicit all-scope permission.

## Out of scope
Branch semantics until OQ-5 is resolved.

## Acceptance Criteria
- AC-608.1: Same-department users see their permitted report data.
- AC-608.2: Administrators see all permitted data.
- AC-608.3: Cross-department access is rejected with the standard forbidden envelope.

## Design
Centralize scope policy and apply it to every report and export query. Original story: `EPIC-08-US-608-report-scoping.md` / AC-608.
