# US-911 · KPI Catalogue Gap Closure

| Field | Value |
|---|---|
| **Story** | `US-911` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-29`](../delivery-plan.md#feat-29) |
| **Layer** | Document / Backend |
| **Ships with** | [US-908](./US-908-executive-dashboard-backend.md) *(Backend)* |
| **Actor** | Supervisor |
| **Priority** | P1 |
| **Sprint** | 18 — BI |
| **Estimate** | 2 points |
| **Status** | `not started` |

## Story

**As a manager**, **I want** a KPI catalogue where every metric is honestly answered or honestly
blocked, **so that** nobody reads a report expecting a metric the data cannot produce.

## Business rules

- The catalogue is the complete list; a KPI not answerable has a named blocker (CSAT has no rating
  collection, real-time streaming is out of scope, drill-down filters are deferred).
- The executive dashboard maps directly onto it; the CSAT card renders the unavailable state.

## Acceptance criteria

#### AC1 — Every KPI answered or blocked

Given the KPI catalogue, then each row is implemented with a named source or marked blocked-with-
reason; none silently absent.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Review | `KpiCatalogue_MatchesExecutiveDashboard` | dashboard fields ⊆ catalogue |

## SQL tables

None.

## Notes

The catalogue lives in the Phase 2 design spec; this story is the check that the shipped endpoints
and screens line up with it.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.