# US-307 · Departments Controller (CRUD)

| Field | Value |
|---|---|
| **Story** | `US-307` |
| **Epic** | [EPIC-12 Platform](../epics/EPIC-12.md) |
| **Feature** | [`FEAT-16` Organisation structure](../delivery-plan.md#feat-16--organisation-structure) |
| **Layer** | Backend |
| **Ships with** | — |
| **Actor** | Admin |
| **Priority** | P0 |
| **Sprint** | [7 — Organisation structure](../delivery-plan.md#sprint-7-organisation-structure) · Slice S8 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-12.7 |
| **Spec criteria** | AC-18 |
| **Depends on** | [US-301](./US-301-department-entity.md), [US-303](./US-303-department-foreign-keys.md) |

## Story

**As an admin**, **I want** to manage departments through an API, **so that** I can create, update, and deactivate departments.

## Business rules

- No BRD BR-n covers this directly. Department grouping.

## Acceptance criteria

#### AC1 — CRUD endpoints for departments (AC-18)

Given an admin is authenticated, when calling the Departments API, then `GET /api/departments`, `POST /api/departments`, `PUT /api/departments/{id}`, and `DELETE /api/departments/{id}` are available and return correct responses.

#### AC2 — Non-admin access denied (AC-18)

Given a non-admin user is authenticated, when calling any Departments mutation endpoint, then a `403 Forbidden` response is returned.

## SQL tables

None — operates on `Departments` (US-301).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-18 | Integration | `GetDepartmentsReturnsList` | Given departments exist, when `GET /api/departments` is called, then a paginated list is returned with `200 OK` | 200 with department list |
| TC-02 | AC-18 | Integration | `PostDepartmentCreatesRecord` | Given a valid department payload, when `POST /api/departments` is called, then the department is created and returned with `201 Created` | 201 with created department |
| TC-03 | AC-18 | Integration | `PutDepartmentUpdatesRecord` | Given a valid department exists, when `PUT /api/departments/{id}` is called with a valid payload, then the department is updated and returned with `200 OK` | 200 with updated department |
| TC-04 | AC-18 | Integration | `DeleteDepartmentReturnsNoContent` | Given a department exists, when `DELETE /api/departments/{id}` is called, then the department is soft-deleted and returns `204 No Content` | 204 No Content |
| TC-05 | AC-18 | Integration | `NonAdminAccessDenied` | Given a non-admin user, when calling `POST /api/departments`, then `403 Forbidden` is returned | 403 Forbidden |

## Notes

- Follow the existing controller conventions in `CustomerSupport.InternalApi`.
- Use MediatR features (CQRS) for command/query separation.
- Delete should be soft-delete (set `IsActive = false`) unless specified otherwise.

## Open questions

None.

## Status evidence

Shipped `FEAT-16` — `DepartmentsController`, full CRUD, Admin-gated mutations, `Authenticated`-gated
reads (`AC-119`, `AC-120`, `AC-123`). Caught and fixed a real gap during implementation: new
`SystemCode`/`SystemCodeMap` entries and a paired `IDbExceptionTranslator` unique-name check were
both missing initially (would have 400'd on unknown id instead of 404, and 500'd on duplicate name
instead of 409) — see the README's "Deviation found and fixed" section.
`docs/superpowers/plans/EPIC-12-US-000-feat-16-organisation-structure/README.md`.

Status is set from what is committed and executed, never from what is planned.
