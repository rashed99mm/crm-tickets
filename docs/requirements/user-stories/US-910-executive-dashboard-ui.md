# US-910 · Executive Dashboard UI

| Field | Value |
|---|---|
| **Story** | `US-910` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-29`](../delivery-plan.md#feat-29) |
| **Layer** | Frontend |
| **Ships with** | [US-908](./US-908-executive-dashboard-backend.md) *(Backend)* |
| **Actor** | Supervisor, Admin |
| **Priority** | P1 |
| **Sprint** | 18 — BI |
| **Estimate** | 5 points |
| **Status** | `not started` |

## Story

**As a supervisor**, **I want** a visual dashboard of the operation, **so that** I can read the state
at a glance rather than opening three report pages.

## Business rules

- Renders from `GET /api/reports/executive-dashboard`; each metric card is an `AsyncState` view —
  never fabricated numbers. CSAT card shows the unavailable state.
- Shared date-range filter re-fetches and the range lives in the URL; route guarded to
  Supervisor/Admin.
- Visualisations are inline SVG/Tailwind pods and bars — no chart library.

## Acceptance criteria

#### AC1 — Dashboard renders

Given a Supervisor/Admin loads the dashboard for the default range, then summary cards, a volume
pods/sparkline list, an SLA bar per priority and a top-agents list render from real endpoint data.

#### AC2 — Range filter + URL

Given the dashboard, when the user sets a from/to range, then it re-fetches and the params are
reflected in the URL.

#### AC3 — Guarded

Given an Agent navigates to the reports routes, then the screens are not reachable.

#### AC4 — Report visuals

Given the report components, then volume/SLA/agent render as visual components, not bare lists.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Component | `ExecutiveDashboard_RendersCardsFromData` | cards from fixture |
| TC-02 | AC2 | Component | `ExecutiveDashboard_ApplyRange_Refetches` | refetch + URL params |
| TC-03 | AC3 | Component | `ExecutiveDashboard_RouteGuard_BlocksAgent` | navigated to forbidden |
| TC-04 | AC1 | Component | `ExecutiveDashboard_Empty_NeverLooksLikeError` | empty ≠ error |

## SQL tables

None.

## Notes

New executive-dashboard screen (admin-app) over the reports API, nav entry under "Reports". Reuses
the shared date-range filter.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.