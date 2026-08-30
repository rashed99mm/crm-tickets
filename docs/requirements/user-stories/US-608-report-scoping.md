# US-608 · Report Scoping

| Field | Value |
|---|---|
| **Story** | `US-608` |
| **Epic** | [EPIC-08 Reports & Management](../epics/EPIC-08-reporting.md) |
| **Feature** | [`FEAT-19` Reporting](../delivery-plan.md#feat-19--reporting) |
| **Layer** | Backend |
| **Ships with** | [US-601](./US-601-reports-controller.md) *(Backend)* |
| **Actor** | Manager |
| **Priority** | P0 |
| **Sprint** | [13 — Reporting](../delivery-plan.md#sprint-13-reporting) · Slice S6 |
| **Estimate** | 3 points |
| **Status** | `partial` |
| **BRD requirements** | FR-9.8 |
| **Spec criteria** | AC-608 |
| **Depends on** | [US-601](./US-601-reports-controller.md) *(Backend)* |

## Story

**As a manager**, **I want** reports scoped to my department, **so that** sensitive data from other departments is not leaked.

## Business rules

- No BRD BR-n covers this directly. Every report query automatically filters by the authenticated user's department scope.
- No BRD BR-n covers this directly. A manager with a `departmentId` claim can only see data for their own department. An admin sees all departments.
- BR-21: Report results are branch-scoped by default; the caller's branch is enforced via JWT claims.

## Acceptance criteria

#### AC1 — Department-scoped queries (spec AC-608)

Given a manager belonging to Department A, when they request any report, then only Department A data is returned.

#### AC2 — Admin sees all (spec AC-608)

Given an admin user, when they request any report, then data from all departments is returned.

#### AC3 — Cross-department access denied (spec AC-608)

Given a manager belonging to Department A, when they attempt to request a report with a Department B filter parameter, then the filter is ignored and Department A data is returned.

## SQL tables

None — read-only query over existing tables.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-608 | Integration | `ReportScopedToDepartment` | Given a manager in Dept A with Dept A tickets, when they request a report, then only Dept A tickets appear. | Department-scoped results |
| TC-02 | AC-608 | Integration | `AdminReportSeesAll` | Given an admin, when they request a report, then tickets from all departments appear. | All departments returned |
| TC-03 | AC-608 | Integration | `CrossDepartmentFilterIgnored` | Given a manager in Dept A, when they pass `departmentId=B` as a query parameter, then only Dept A data is returned. | Filter parameter ignored |

## Notes

Scoping is enforced in the application layer via a `DepartmentScopeBehavior` pipeline behavior or handler filter. The user's `departmentId` comes from their JWT claims.

## Open questions

None.

## Status evidence

**Adapted, not built as specced** (spec addendum A1, `EPIC-08-US-606-reporting.md`): this
codebase has no `Manager` role and no `departmentId` JWT claim, and `Ticket.DepartmentId`/
`ApplicationUser.DepartmentId` are never populated by anything, so a department filter would
filter on a column that is always null. What shipped instead: every report endpoint is gated to
`Admin`/`Supervisor` at the controller (`AC-148`, tested) — the access-control half of this story.
The department-scoping half is not built and not planned without a product decision on the role/
claim gap it depends on.

Status is set from what is committed and executed, never from what is planned.
