# US-915 · Agent Dashboard: Workflow-Driven

| Field | Value |
|---|---|
| **Story** | `US-915` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-30`](../delivery-plan.md#feat-30) |
| **Layer** | Frontend |
| **Ships with** | [US-901](./US-901-real-life-8-state-lifecycle.md) *(Backend)* |
| **Actor** | Agent |
| **Priority** | P1 |
| **Sprint** | 19 — UX redesign |
| **Estimate** | 4 points |
| **Status** | `not started` |

## Story

**As an agent**, **I want** a dashboard organised by where my work actually is, **so that** I start
from "what needs me now" instead of a generic list.

## Business rules

- Sections follow the workflow: waiting-on-me (waiting states), in progress, new/unassigned-to-pick,
  with SLA countdown and an activity strip — all real data.
- Escalated tickets surface with the marker.
- Unassigned rail remains supervisor-or-admin only.

## Acceptance criteria

#### AC1 — Workflow-driven dashboard

Given a logged-in agent, then the dashboard shows waiting-on-me, in-progress and pickable sections
from real data, with SLA countdown, and the unassigned rail only for supervisors/admins.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Component | `Dashboard_Sections_FollowWorkflow` | three workflow sections from fixture |
| TC-02 | AC1 | Component | `Dashboard_UnassignedRail_SupervisorOnly` | hidden for agent |
| TC-03 | AC1 | Component | `Dashboard_SlaCountdown_Shown` | real due-date countdown |

## SQL tables

None.

## Notes

Reworks the agent dashboard; count tiles move to the new status groupings.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.