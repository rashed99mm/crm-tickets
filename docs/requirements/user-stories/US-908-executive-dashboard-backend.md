# US-908 · Executive Dashboard Backend

| Field | Value |
|---|---|
| **Story** | `US-908` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-29`](../delivery-plan.md#feat-29) |
| **Layer** | Backend |
| **Ships with** | [US-910](./US-910-executive-dashboard-ui.md) *(Frontend)* |
| **Actor** | Supervisor, Admin |
| **Priority** | P1 |
| **Sprint** | 18 — BI |
| **Estimate** | 5 points |
| **Status** | `not started` |

## Story

**As a supervisor**, **I want** one screen showing the state of the operation at a glance, **so that**
I see volume, SLA and load from a single endpoint.

## Business rules

- New `GET /api/reports/executive-dashboard?from&to` behind the existing `Supervisor` policy
  (Supervisor OR Admin).
- Response: `ticketsCreated`, `openNow`, `unassigned`, `breachedSla`, `timeToFirstResponseMinutes`
  (mean), `resolutionRate`, `avgHandleMinutes`, plus `escalated`.
- Every KPI either ships with a named data source or is listed as not-answerable with its blocker.
  CSAT present as unavailable.

## Acceptance criteria

#### AC1 — Executive dashboard endpoint

Given a Supervisor or Admin, when `GET /api/reports/executive-dashboard?from&to` is called, then the
response contains the seven metrics computed from committed tables for the range.

#### AC2 — Escalation KPI

Given tickets with `EscalationState != "None"` in range, then the escalation count/share is reported
without schema additions.

#### AC3 — Catalogue completeness

Given the KPI catalogue, then each KPI is implemented or blocked-with-reason; none silently missing.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Integration | `ExecutiveDashboard_ReturnsSevenMetrics` | 200, correct field values |
| TC-02 | AC1 | Integration | `Agent_CannotReadExecutiveDashboard` | 403 |
| TC-03 | AC2 | Integration | `ExecutiveDashboard_EscalationCount` | correct escalated share |
| TC-04 | AC1 | Integration | `FromAfterTo_Returns400` | `To` field error |

## SQL tables

None — aggregation over `Ticket`/`SLAEvent`/`TicketMessage` (existing in-memory pattern).

## Notes

New executive-dashboard query and response model behind the reports area, range validation reusing
the existing report-range error. See FEAT-29 plan.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.