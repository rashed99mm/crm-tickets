# US-309 · Department Management Screen

| Field | Value |
|---|---|
| **Story** | `US-309` |
| **Epic** | [EPIC-12 Platform](../epics/EPIC-12.md) |
| **Feature** | [`FEAT-16` Organisation structure](../delivery-plan.md#feat-16--organisation-structure) |
| **Layer** | Frontend |
| **Ships with** | [US-307](./US-307-departments-controller.md) *(layer)* |
| **Actor** | Admin |
| **Priority** | P0 |
| **Sprint** | [7 — Organisation structure](../delivery-plan.md#sprint-7-organisation-structure) · Slice S8 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-12.7 |
| **Spec criteria** | AC-20 |
| **Depends on** | [US-307](./US-307-departments-controller.md) |

## Story

**As an admin**, **I want** to manage departments through a UI, **so that** I can create, update, and deactivate departments without using the API directly.

## Business rules

- No BRD BR-n covers this directly. Department grouping.

## Acceptance criteria

#### AC1 — Department list view (AC-20)

Given an admin navigates to the department management screen, when the page loads, then a table of departments is displayed with columns for Name, Manager, and Active status.

#### AC2 — Create and edit department (AC-20)

Given the admin clicks "Add Department" or "Edit" on a row, when the form is submitted with valid data, then the department is created or updated via the API and the table refreshes.

#### AC3 — Deactivate department (AC-20)

Given the admin clicks "Deactivate" on a department row, when confirmed, then the department is soft-deleted via the API and the table reflects the change.

## SQL tables

None — frontend story.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-20 | Component | `DepartmentListComponentRenders` | Given the component is rendered, when data loads, then a table with department rows is visible | Table rows displayed |
| TC-02 | AC-20 | Component | `CreateDepartmentCallsApi` | Given the create form is filled and submitted, when the API call fires, then `POST /api/departments` is called with correct payload | API called, table refreshed |
| TC-03 | AC-20 | Component | `EditDepartmentCallsApi` | Given the edit form is filled and submitted, when the API call fires, then `PUT /api/departments/{id}` is called with correct payload | API called, table refreshed |
| TC-04 | AC-20 | Component | `DeactivateDepartmentCallsApi` | Given the deactivate button is clicked and confirmed, when the API call fires, then `DELETE /api/departments/{id}` is called | API called, row removed or marked inactive |

## Notes

- Follow Angular standalone component conventions in the admin-app.
- Use signals for reactive state.
- Follow existing CRUD screen patterns in the admin app.

## Open questions

None.

## Status evidence

Shipped `FEAT-16` — `DepartmentsComponent`, wired into `app.routes.ts` and the shell nav. Shipped
despite the spec's own A2 having cut it for time; scope was revised mid-implementation on explicit
instruction to ship the epic end to end rather than backend-only. No component test proven per-AC
(README's own recorded gap) — manually build-verified only. See
`docs/superpowers/plans/EPIC-12-US-000-feat-16-organisation-structure/README.md`.

Status is set from what is committed and executed, never from what is planned.
