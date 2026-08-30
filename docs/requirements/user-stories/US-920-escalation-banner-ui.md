# US-920 · Escalation Banner + Specialist Hand-off UI

| Field | Value |
|---|---|
| **Story** | `US-920` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-30`](../delivery-plan.md#feat-30) |
| **Layer** | Frontend |
| **Ships with** | [US-904](./US-904-escalation-owner-and-marker.md) *(Backend)* |
| **Actor** | Supervisor |
| **Priority** | P1 |
| **Sprint** | 19 — UX redesign |
| **Estimate** | 3 points |
| **Status** | `not started` |

## Story

**As a supervisor**, **I want** to see escalation at a glance and hand an escalated ticket to a
specialist, **so that** escalation is a visible, owned workflow.

## Business rules

- Banner on ticket detail when `EscalationState != "None"` shows level + owner; a hand-off control
  targets a named Specialist.
- Queue and dashboard surfaces show the existing escalation badge tied to the new marker data.

## Acceptance criteria

#### AC1 — Banner + hand-off

Given a supervisor views an escalated ticket, then a banner shows level + owner and a hand-off control
moves it to a named Specialist.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Component | `EscalationBanner_ShowsLevelOwner` | banner from fixture |
| TC-02 | AC1 | Component | `HandOff_ToSpecialist_Submits` | POST to escalation-owner |

## SQL tables

None.

## Notes

Uses `EscalationAssigneeId` from US-904 DTOs; associates with the escalation hand-off endpoint.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.