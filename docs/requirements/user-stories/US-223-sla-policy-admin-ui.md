# US-223 · SLA Policy Management Screen

| Field | Value |
|---|---|
| **Story** | `US-223` |
| **Epic** | [EPIC-04 Agent Dashboard](../epics/EPIC-04-agent-dashboard.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Frontend |
| **Ships with** | [US-214](./US-214-sla-policies-crud.md) *(frontend)* |
| **Actor** | Admin |
| **Priority** | P0 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-5.1 |
| **Spec criteria** | AC-5.1 |
| **Depends on** | [US-214](./US-214-sla-policies-crud.md), [US-011](./US-011-ticket-detail-screen.md) |

## Story

**As an admin**, **I want** to manage SLA policies through a UI, **so that** response and resolution targets are configurable without code changes.

## Business rules

- BR-29 — SLA policy UI is accessible only to administrators (BRD).

## Acceptance criteria

#### AC1 — List SLA Policies (spec AC-5.1)

Given an admin navigates to the SLA policy screen, when the screen loads, then all active SLA policies are displayed in a table.

#### AC2 — Create/Edit SLA Policy (spec AC-5.1)

Given an admin opens the policy form, when valid values are submitted, then the policy is created or updated via the API and reflected in the list.

#### AC3 — Delete SLA Policy (spec AC-5.1)

Given an admin deletes a policy, when confirmation is provided, then the policy is soft-deleted and removed from the active list.

## SQL tables

None — frontend story, consumes `SLAPolicies` via API from [US-214](./US-214-sla-policies-crud.md).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5.1 | E2E | `SLAPolicyScreen_ShouldListPolicies` | Given an admin is logged in, when the SLA policy screen is opened, then all active policies are displayed in a table | Table with policies visible |
| TC-02 | AC-5.1 | E2E | `SLAPolicyScreen_ShouldCreatePolicy` | Given an admin submits a valid policy form, when the form is submitted, then the policy is created and appears in the list | New policy appears in list |
| TC-03 | AC-5.1 | Unit | `SLAPolicyForm_ShouldValidateRequiredFields` | Given an empty policy form is submitted, when validation runs, then validation errors are shown for required fields | Validation messages displayed |

## Notes

Uses Angular standalone component with reactive forms. Connects to `/api/SLAPolicies` endpoints from [US-214](./US-214-sla-policies-crud.md). Table follows existing admin UI patterns from the reference platform.

## Open questions

None.

## Status evidence

List and create shipped earlier (`FEAT-17` first slice, `SLAPoliciesComponent`). The edit gap
(AC2) that slice's own task record explicitly left open closed 2026-08-27 — an edit form reusing
`SLAPolicyApi.update`, previously wired but unused. `npx ng test admin-app --watch=false
--include='**/sla-policies.component.spec.ts'` → passing (part of a combined 22/22 run). See
`docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-escalation/README.md`'s "Frontend addendum".
**Not yet committed** — staged only, per explicit instruction this session.

Status is set from what is committed and executed, never from what is planned.
