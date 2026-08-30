# US-601 · Reports Controller

| Field | Value |
|---|---|
| **Story** | `US-601` |
| **Epic** | [EPIC-08 Reports & Management](../epics/EPIC-08-reporting.md) |
| **Feature** | [`FEAT-19` Reporting](../delivery-plan.md#feat-19--reporting) |
| **Layer** | Backend |
| **Ships with** | [US-602](./US-602-ticket-volume-report.md) *(Backend)*, [US-603](./US-603-sla-performance-report.md) *(Backend)*, [US-604](./US-604-agent-performance-report.md) *(Backend)*, [US-605](./EPIC-08-US-605-csat-report.md) *(Backend)* |
| **Actor** | Manager |
| **Priority** | P0 |
| **Sprint** | [13 — Reporting](../delivery-plan.md#sprint-13-reporting) · Slice S6 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-9.1 |
| **Spec criteria** | AC-601 |
| **Depends on** | [US-301](./US-301-department-entity.md) *(Backend)*, [US-302](./US-302-branch-entity.md) *(Backend)* |

## Story

**As a manager**, **I want** reporting endpoints hosted on the InternalApi, **so that** reporting data is accessible to authorised staff.

## Business rules

- No BRD BR-n covers this directly.
- BR-21: All report queries are branch-scoped by default; the caller's branch is enforced via JWT claims.

## Acceptance criteria

#### AC1 — ReportsController exposes report endpoints (spec AC-601)

Given the InternalApi is running, when a manager calls any report endpoint, then the controller returns the correct report data with proper pagination and date-range filtering.

## SQL tables

None — read-only query over existing tables.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-601 | Integration | `ReportsControllerReturnsOk` | Given an authenticated manager, when they call `GET /api/reports/ticket-volume`, then a 200 response is returned with report data. | 200 OK with valid JSON report payload |

## Notes

ReportsController lives in `CustomerSupport.InternalApi` and delegates to application-layer handlers. No report endpoints on ExternalApi.

## Open questions

None.

## Status evidence

Shipped — `ReportsController`, `[Authorize(Policy = "Supervisor")]` (Supervisor-or-Admin per
`AuthorizationExtensions.cs`), three actions. `AC148_Agent_CannotReadTicketVolumeReport` and
`AC148_Unauthenticated_CannotReadTicketVolumeReport` in `ReportsEndpointTests.cs` pass. Full
evidence in `docs/superpowers/plans/EPIC-08-US-606-feat-reporting/README.md`.

Status is set from what is committed and executed, never from what is planned.
