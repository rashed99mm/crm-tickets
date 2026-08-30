# US-214 · SLA Policies Controller (CRUD)

| Field | Value |
|---|---|
| **Story** | `US-214` |
| **Epic** | [EPIC-05 SLA & Escalation](../epics/EPIC-05.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Backend |
| **Ships with** | [US-223](./US-223-sla-policies-admin-ui.md) *(Frontend)* |
| **Actor** | Admin |
| **Priority** | P0 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-5.1, BR-01 |
| **Spec criteria** | AC-5.1 |
| **Depends on** | [US-210](./US-210-sla-policy-entity.md) |

## Story

**As an admin**, **I want** to manage SLA policies, **so that** targets are configurable.

## Business rules

- BR-01 — Only administrators can manage SLA policies (BRD).
- BR-02 — Policy priority + category + branch combination must be unique per active policy (BRD).

## Acceptance criteria

#### AC1 — CRUD Operations on SLA Policies (spec AC-5.1)

Given an admin is authenticated, when CRUD operations are performed on /api/SLAPolicies, then policies are created, read, updated, and deleted according to the operation.

## SQL tables

Uses `SLAPolicies` table from [US-210](./US-210-sla-policy-entity.md).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5.1 | Integration | `GetPolicies_ShouldReturnAll` | Given admin is authenticated, when GET /api/SLAPolicies is called, then all active policies are returned | 200 OK with list |
| TC-02 | AC-5.1 | Integration | `CreatePolicy_ShouldReturnCreated` | Given admin is authenticated, when POST /api/SLAPolicies with valid payload is called, then policy is created | 201 Created with policy |
| TC-03 | AC-5.1 | Integration | `UpdatePolicy_ShouldReturnUpdated` | Given admin is authenticated, when PUT /api/SLAPolicies/{id} with valid payload is called, then policy is updated | 200 OK with updated policy |
| TC-04 | AC-5.1 | Integration | `DeletePolicy_ShouldReturnNoContent` | Given admin is authenticated, when DELETE /api/SLAPolicies/{id} is called, then policy is soft-deleted | 204 No Content |
| TC-05 | AC-5.1 | Integration | `NonAdmin_ShouldReturnForbidden` | Given non-admin user, when POST /api/SLAPolicies is called, then access is denied | 403 Forbidden |

## Notes

Endpoint must enforce Admin role via [Authorize] attribute. Deletion should be soft-delete (IsActive = false) to preserve SLA history on existing tickets.

## Open questions

None.

## Status evidence

Backend shipped across `FEAT-17`'s two slices — `SLAPoliciesController` gained `PUT`/`DELETE
/api/SLAPolicies/{id}` (Admin-gated, 404 on unknown id) on top of the first slice's create+list,
closing what that slice's own task record had marked open. Frontend counterpart `US-223` also done
(edit form, 2026-08-27). See `docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-tracking/README.md`'s
"Final state" section.

Status is set from what is committed and executed, never from what is planned.
