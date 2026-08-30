# US-902 · SLA Pause on Both Waiting States

| Field | Value |
|---|---|
| **Story** | `US-902` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-28`](../delivery-plan.md#feat-28) |
| **Layer** | Backend |
| **Ships with** | [US-901](./US-901-real-life-8-state-lifecycle.md) *(Backend)* |
| **Actor** | Agent |
| **Priority** | P0 |
| **Sprint** | 17 — Phase 2 workflow |
| **Estimate** | 3 points |
| **Status** | `not started` |

## Story

**As an agent**, **I want** time spent waiting on the customer or on an internal team excluded from
the SLA clock, **so that** SLA attainment measures productive work, not waiting.

## Business rules

- Entering either waiting state starts the pause (`PausedAt` set); leaving it accumulates the span
  into `TotalPausedSeconds` and shifts `ResponseDueAt`/`ResolutionDueAt` forward by the span.
- The SLA pause logic keys on both waiting statuses, not the literal `"Pending"`.

## Acceptance criteria

#### AC1 — Pause starts and resumes

Given a ticket in `In Progress`, when moved to `Waiting for Customer` or `Waiting for Internal Team`,
then `PausedAt` is set; when it leaves the waiting state, then the elapsed span accumulates and both
due dates shift forward by the span.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Unit | `Ticket_SlaPause_WaitingForCustomer_ShiftsDues` | `PausedAt` set on entry; due dates extended by span on exit |
| TC-02 | AC1 | Unit | `Ticket_SlaPause_WaitingForInternal_ShiftsDues` | same for internal waiting |
| TC-03 | AC1 | Integration | `SlaPause_AccumulatesAcrossCycles` | two waiting cycles accumulate `TotalPausedSeconds` |

## SQL tables

None.

## Notes

Replaces the hard-coded `"Pending"` branch; updates existing `AC-134…136` pause tests to the new status
names.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.