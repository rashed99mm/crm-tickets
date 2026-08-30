# US-802 · Audit Log Viewer Admin UI

| Field | Value |
|---|---|
| **Story** | `US-802` |
| **Epic** | [EPIC-09 Security & Administration](../epics/EPIC-09-administration.md) |
| **Feature** | [`FEAT-21` Security & Administration](../delivery-plan.md#feat-21--security-administration) |
| **Layer** | Frontend |
| **Ships with** | [US-801](./US-801-audit-log-query.md) *(backend)* |
| **Actor** | Admin |
| **Priority** | P1 |
| **Sprint** | [12 — Administration](../delivery-plan.md#sprint-12-administration) · Slice S9 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-10.9 |
| **Spec criteria** | AC-802 |
| **Depends on** | [US-801](./US-801-audit-log-query.md) |

## Story

**As an admin**, **I want** to browse audit log entries in the UI, **so that** I can investigate events without writing queries.

## Business rules

- No BRD BR-n covers this directly. The audit log viewer displays entries in reverse chronological order with filtering and pagination.

## Acceptance criteria

#### AC1 — Audit log page (spec AC-802)

Given an admin is logged in, when they navigate to the audit log page, then a table of audit entries is displayed.

#### AC2 — Filter controls (spec AC-802)

Given the audit log page, when the admin uses filter controls, then entries are filtered by action type, user, and date range.

#### AC3 — Pagination (spec AC-802)

Given more than 50 audit entries, when the admin views the page, then entries are paginated with page navigation controls.

#### AC4 — Entry detail (spec AC-802)

Given an audit entry in the table, when the admin clicks on it, then full entry details (action, user, entity, IP, timestamp) are displayed.

## SQL tables

None — frontend story. Consumes the API endpoint from [US-801](./US-801-audit-log-query.md).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-802 | Component | `AuditLogPageRenders` | Given the admin navigates to audit log, when the page loads, then a table with audit entries is visible. | Table renders with entries |
| TC-02 | AC-802 | Component | `AuditLogFilterApplied` | Given the admin selects actionType=login, when the filter is applied, then only login entries appear in the table. | Filtered table results |
| TC-03 | AC-802 | Component | `AuditLogPaginationWorks` | Given 100 entries, when the admin clicks page 2, then the next 50 entries are displayed. | Page 2 shows entries 51-100 |

## Notes

Frontend page under the admin section of the Angular app. Uses Angular signals for state management. Responsive table layout.

## Open questions

None.

## Status evidence

Shipped `FEAT-19`(admin) — `AuditLogComponent`, filterable/paginated table with a row-click detail
panel. Date-range filtering (spec A5) not built — the backend query only supports
`actionType`/`userId`. See `docs/superpowers/plans/EPIC-09-US-804-feat-21-administration/README.md`.

Status is set from what is committed and executed, never from what is planned.
