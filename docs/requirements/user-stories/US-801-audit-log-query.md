# US-801 · Audit Log Query Endpoint

| Field | Value |
|---|---|
| **Story** | `US-801` |
| **Epic** | [EPIC-09 Security & Administration](../epics/EPIC-09-administration.md) |
| **Feature** | [`FEAT-21` Security & Administration](../delivery-plan.md#feat-21--security-administration) |
| **Layer** | Backend |
| **Ships with** | [US-802](./US-802-audit-log-viewer.md) *(frontend)* |
| **Actor** | Admin |
| **Priority** | P1 |
| **Sprint** | [12 — Administration](../delivery-plan.md#sprint-12-administration) · Slice S9 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-10.9 |
| **Spec criteria** | AC-801 |
| **Depends on** | [US-301](./US-301-department-entity.md) |

## Story

**As an admin**, **I want** to query the audit log through an API endpoint, **so that** I can investigate security events and compliance violations.

## Business rules

- No BRD BR-n covers this directly. Audit log queries are restricted to admin role only.
- No BRD BR-n covers this directly. Audit entries are immutable; the query endpoint is read-only.

## Acceptance criteria

#### AC1 — Audit query endpoint (spec AC-801)

Given an admin is authenticated, when they call `GET /api/admin/audit-log`, then paginated audit entries are returned.

#### AC2 — Filtering by action type (spec AC-801)

Given an admin, when they query with `actionType=login`, then only login-related audit entries are returned.

#### AC3 — Filtering by user (spec AC-801)

Given an admin, when they query with `userId=xxx`, then only audit entries for that user are returned.

#### AC4 — Non-admin access denied (spec AC-801)

Given a non-admin user, when they attempt to query the audit log, then a 403 Forbidden response is returned.

## SQL tables

None — read-only query over existing `AuditLog` table.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-801 | Integration | `AuditLogEndpointReturnsEntries` | Given audit entries exist, when an admin calls the endpoint, then a paginated list is returned. | 200 OK with audit entries |
| TC-02 | AC-801 | Integration | `AuditLogFilterByAction` | Given 10 entries (5 login, 5 update), when filtering by actionType=login, then 5 entries returned. | Filtered results |
| TC-03 | AC-801 | Integration | `AuditLogNonAdminDenied` | Given a non-admin user, when they call the endpoint, then 403 is returned. | 403 Forbidden |

## Notes

Endpoint lives on `AdminController` in InternalApi. Supports pagination via `skip`/`take` query parameters.

## Open questions

None.

## Status evidence

Shipped `FEAT-19`(admin) — `GET /api/admin/audit-log`, Admin-gated, filterable by
`actionType`/`userId`, newest-first via an explicit `SortBy`/`SortDirection` override (the
repository's `GetPagedAsync` has no default ordering when `SortBy` is unset). Also fixed a real
dead-code bug found while scoping this story: `AuditBehavior` was never registered in the MediatR
pipeline and, even if it had been, never called `IAuditService.LogAsync` — the `AuditLogs` table
had been permanently empty since the platform was adopted. Both fixed.
`AuditLogEndpointTests` 6/6, full suite 351/351. See
`docs/superpowers/plans/EPIC-09-US-804-feat-21-administration/README.md`.

Status is set from what is committed and executed, never from what is planned.
