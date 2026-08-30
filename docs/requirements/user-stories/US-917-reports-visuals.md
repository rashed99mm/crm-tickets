# US-917 · Reports Visuals (Inline SVG, No Chart Lib)

| Field | Value |
|---|---|
| **Story** | `US-917` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-30`](../delivery-plan.md#feat-30) |
| **Layer** | Frontend |
| **Ships with** | [US-910](./US-910-executive-dashboard-ui.md) *(Frontend)* |
| **Actor** | Supervisor, Admin |
| **Priority** | P1 |
| **Sprint** | 19 — UX redesign |
| **Estimate** | 5 points |
| **Status** | `not started` |

## Story

**As a supervisor**, **I want** report numbers to render as visuals, **so that** trends read at a
glance instead of as three `<ul>`s.

## Business rules

- Volume/SLA/agent reports render bars/pods via inline SVG (no chart dependency).
- Shared date-range filter on each; executive dashboard links to them.
- Keeps the `AsyncState` loading/empty/error contract and i18n.

## Acceptance criteria

#### AC1 — Visual report components

Given the report screens, then volume/SLA/agent render as visual components with a shared date filter
and the dashboard links to them.

#### AC2 — Breakdowns not bare lists

Given the ticket-volume report, then each breakdown is visual.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Component | `TicketVolumeReport_RendersBars` | SVG bars from fixture |
| TC-02 | AC1 | Component | `Reports_ShareDateRangeFilter` | filter emits apply |
| TC-03 | AC1 | Component | `SlaReport_PriorityBars_Render` | per-priority bar |

## SQL tables

None.

## Notes

Visual primitives are small SVG bar/pod components reused by all report screens. No charting
dependency added.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.