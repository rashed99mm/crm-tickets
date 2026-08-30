# US-310 · Branch Management Screen

| Field | Value |
|---|---|
| **Story** | `US-310` |
| **Epic** | [EPIC-12 Platform](../epics/EPIC-12.md) |
| **Feature** | [`FEAT-16` Organisation structure](../delivery-plan.md#feat-16--organisation-structure) |
| **Layer** | Frontend |
| **Ships with** | [US-308](./US-308-branches-controller.md) *(layer)* |
| **Actor** | Admin |
| **Priority** | P0 |
| **Sprint** | [7 — Organisation structure](../delivery-plan.md#sprint-7-organisation-structure) · Slice S8 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-12.8 |
| **Spec criteria** | AC-21 |
| **Depends on** | [US-308](./US-308-branches-controller.md) |

## Story

**As an admin**, **I want** to manage branches through a UI, **so that** I can create, update, and deactivate branches without using the API directly.

## Business rules

- No BRD BR-n covers this directly. Branch location grouping.

## Acceptance criteria

#### AC1 — Branch list view (AC-21)

Given an admin navigates to the branch management screen, when the page loads, then a table of branches is displayed with columns for Name, Region, Timezone, and Active status.

#### AC2 — Create and edit branch (AC-21)

Given the admin clicks "Add Branch" or "Edit" on a row, when the form is submitted with valid data, then the branch is created or updated via the API and the table refreshes.

#### AC3 — Deactivate branch (AC-21)

Given the admin clicks "Deactivate" on a branch row, when confirmed, then the branch is soft-deleted via the API and the table reflects the change.

## SQL tables

None — frontend story.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-21 | Component | `BranchListComponentRenders` | Given the component is rendered, when data loads, then a table with branch rows is visible | Table rows displayed |
| TC-02 | AC-21 | Component | `CreateBranchCallsApi` | Given the create form is filled and submitted, when the API call fires, then `POST /api/branches` is called with correct payload | API called, table refreshed |
| TC-03 | AC-21 | Component | `EditBranchCallsApi` | Given the edit form is filled and submitted, when the API call fires, then `PUT /api/branches/{id}` is called with correct payload | API called, table refreshed |
| TC-04 | AC-21 | Component | `DeactivateBranchCallsApi` | Given the deactivate button is clicked and confirmed, when the API call fires, then `DELETE /api/branches/{id}` is called | API called, row removed or marked inactive |

## Notes

- Follow Angular standalone component conventions in the admin-app.
- Use signals for reactive state.
- Follow existing CRUD screen patterns in the admin app.

## Open questions

None.

## Status evidence

**Not built.** Never specced (the frontend spec's `US-309` covers Department only) — would be the
same shape as `DepartmentsComponent` if wanted. See
`docs/superpowers/plans/EPIC-12-US-000-feat-16-organisation-structure/README.md`.

Status is set from what is committed and executed, never from what is planned.
