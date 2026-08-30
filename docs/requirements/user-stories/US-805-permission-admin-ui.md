# US-805 · Permission Management Admin UI

| Field | Value |
|---|---|
| **Story** | `US-805` |
| **Epic** | [EPIC-09 Security & Administration](../epics/EPIC-09-administration.md) |
| **Feature** | [`FEAT-21` Security & Administration](../delivery-plan.md#feat-21--security-administration) |
| **Layer** | Frontend |
| **Ships with** | [US-804](./EPIC-09-US-804-permission-entity.md) *(backend)* |
| **Actor** | Admin |
| **Priority** | P1 |
| **Sprint** | [12 — Administration](../delivery-plan.md#sprint-12-administration) · Slice S9 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-10.8 |
| **Spec criteria** | AC-805 |
| **Depends on** | [US-804](./EPIC-09-US-804-permission-entity.md) |

## Story

**As an admin**, **I want** to manage permissions through the UI, **so that** I can view and adjust role-permission mappings without database changes.

## Business rules

- No BRD BR-n covers this directly. The admin can view all permissions, assign permissions to roles, and revoke permissions from roles.
- No BRD BR-n covers this directly. Built-in roles (Admin, Manager, Supervisor, Agent) cannot have all permissions removed — at least one permission must remain.

## Acceptance criteria

#### AC1 — Permission list (spec AC-805)

Given an admin navigates to permission management, when the page loads, then a list of all permissions with their current role assignments is displayed.

#### AC2 — Assign permission to role (spec AC-805)

Given the admin selects a role, when they toggle a permission on, then the permission is added to the role and a success confirmation is shown.

#### AC3 — Revoke permission from role (spec AC-805)

Given the admin selects a role, when they toggle a permission off, then the permission is removed from the role.

#### AC4 — Prevent removing all permissions (spec AC-805)

Given a built-in role with 1 remaining permission, when the admin attempts to remove it, then a warning prevents the action.

## SQL tables

None — frontend story. Consumes the backend entity from [US-804](./EPIC-09-US-804-permission-entity.md) via API.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-805 | Component | `PermissionListRenders` | Given the admin navigates to permissions, when the page loads, then a table of permissions and role assignments is visible. | Permission table renders |
| TC-02 | AC-805 | Component | `AssignPermissionToRole` | Given the admin toggles `ticket.create` on for Agent role, when saved, then the permission appears in Agent's list. | Permission assigned |
| TC-03 | AC-805 | Component | `RevokePermissionFromRole` | Given the admin toggles a permission off, when saved, then the permission is removed from the role. | Permission revoked |
| TC-04 | AC-805 | Component | `CannotRemoveLastPermission` | Given a role with 1 permission, when the admin attempts to remove it, then a warning blocks the action. | Warning displayed, save blocked |

## Notes

Frontend page under admin section. Uses a matrix-style UI (permissions as columns, roles as rows) or a per-role checkbox list. Depends on US-804 backend being complete.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
