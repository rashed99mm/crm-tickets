# US-909 · Report Accuracy Improvements (Timestamp-Based KPIs)

| Field | Value |
|---|---|
| **Story** | `US-909` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-29`](../delivery-plan.md#feat-29) |
| **Layer** | Backend |
| **Ships with** | [US-906](./US-906-lifecycle-timestamps-for-bi.md) *(Backend)* |
| **Actor** | Supervisor, Admin |
| **Priority** | P1 |
| **Sprint** | 18 — BI |
| **Estimate** | 3 points |
| **Status** | `not started` |

## Story

**As a manager**, **I want** the existing reports to use the new lifecycle timestamps, **so that**
first-response and handle time stop being approximations.

## Business rules

- The agent-performance report fix: `avgHandleMinutes` uses
  `(t.ResolvedAt ?? t.UpdatedAt ?? t.CreatedAt) - t.CreatedAt` with SLA pause subtracted.
- The first-response approximation moves to `FirstResponseAt` where present; a ticket with no
  outbound message still has no response time and is excluded, as before.

## Acceptance criteria

#### AC1 — Handle time from timestamps

Given resolved tickets, then `avgHandleMinutes` derives from `ResolvedAt`, not `UpdatedAt`.

#### AC2 — Response time from timestamps

Given tickets with an outbound response in range, then response metrics use `FirstResponseAt`.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Integration | `AgentPerformance_UsesResolvedAt` | matches timestamp arithmetic |
| TC-02 | AC2 | Integration | `SlaReport_FirstResponse_UsesTimestamp` | matches |

## SQL tables

None.

## Notes

Amends the agent-performance `avgHandleMinutes` derivation and the first-response derivations.
Existing report integration tests updated where their fixtures set `UpdatedAt` only.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.