# US-308 · Branches Controller (CRUD)

| Field | Value |
|---|---|
| **Story** | `US-308` |
| **Epic** | [EPIC-12 Platform](../epics/EPIC-12.md) |
| **Feature** | [`FEAT-16` Organisation structure](../delivery-plan.md#feat-16--organisation-structure) |
| **Layer** | Backend |
| **Ships with** | — |
| **Actor** | Admin |
| **Priority** | P0 |
| **Sprint** | [7 — Organisation structure](../delivery-plan.md#sprint-7-organisation-structure) · Slice S8 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-12.8 |
| **Spec criteria** | AC-19 |
| **Depends on** | [US-302](./US-302-branch-entity.md), [US-304](./US-304-branch-foreign-keys.md) |

## Story

**As an admin**, **I want** to manage branches through an API, **so that** I can create, update, and deactivate branches.

## Business rules

- No BRD BR-n covers this directly. Branch location grouping.

## Acceptance criteria

#### AC1 — CRUD endpoints for branches (AC-19)

Given an admin is authenticated, when calling the Branches API, then `GET /api/branches`, `POST /api/branches`, `PUT /api/branches/{id}`, and `DELETE /api/branches/{id}` are available and return correct responses.

#### AC2 — Non-admin access denied (AC-19)

Given a non-admin user is authenticated, when calling any Branches mutation endpoint, then a `403 Forbidden` response is returned.

## SQL tables

None — operates on `Branches` (US-302).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-19 | Integration | `GetBranchesReturnsList` | Given branches exist, when `GET /api/branches` is called, then a paginated list is returned with `200 OK` | 200 with branch list |
| TC-02 | AC-19 | Integration | `PostBranchCreatesRecord` | Given a valid branch payload, when `POST /api/branches` is called, then the branch is created and returned with `201 Created` | 201 with created branch |
| TC-03 | AC-19 | Integration | `PutBranchUpdatesRecord` | Given a valid branch exists, when `PUT /api/branches/{id}` is called with a valid payload, then the branch is updated and returned with `200 OK` | 200 with updated branch |
| TC-04 | AC-19 | Integration | `DeleteBranchReturnsNoContent` | Given a branch exists, when `DELETE /api/branches/{id}` is called, then the branch is soft-deleted and returns `204 No Content` | 204 No Content |
| TC-05 | AC-19 | Integration | `NonAdminAccessDenied` | Given a non-admin user, when calling `POST /api/branches`, then `403 Forbidden` is returned | 403 Forbidden |

## Notes

- Follow the existing controller conventions in `CustomerSupport.InternalApi`.
- Use MediatR features (CQRS) for command/query separation.
- Delete should be soft-delete (set `IsActive = false`) unless specified otherwise.

## Open questions

None.

## Status evidence

Shipped `FEAT-16` — `BranchesController`, same shape and same fixed gaps as `US-307`'s
`DepartmentsController`. See
`docs/superpowers/plans/EPIC-12-US-000-feat-16-organisation-structure/README.md`.

Status is set from what is committed and executed, never from what is planned.
