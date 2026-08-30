# US-604 · Agent Performance Report

| Field | Value |
|---|---|
| **Story** | `US-604` |
| **Epic** | [EPIC-08 Reports & Management](../epics/EPIC-08-reporting.md) |
| **Feature** | [`FEAT-19` Reporting](../delivery-plan.md#feat-19--reporting) |
| **Layer** | Backend |
| **Ships with** | [US-601](./US-601-reports-controller.md) *(Backend)* |
| **Actor** | Supervisor |
| **Priority** | P0 |
| **Sprint** | [13 — Reporting](../delivery-plan.md#sprint-13-reporting) · Slice S6 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-9.3 |
| **Spec criteria** | AC-604 |
| **Depends on** | [US-601](./US-601-reports-controller.md) *(Backend)*, [US-608](./EPIC-08-US-608-report-scoping.md) *(Backend)* |

## Story

**As a supervisor**, **I want** agent throughput and average handle time metrics, **so that** I can manage team performance and identify coaching opportunities.

## Business rules

- No BRD BR-n covers this directly. Agent performance report shows tickets resolved, average handle time, and first-response time per agent.
- BR-21: Report results are branch-scoped by default; the caller's branch is enforced via JWT claims.

## Acceptance criteria

#### AC1 — Agent throughput (spec AC-604)

Given agents have resolved tickets, when the supervisor requests the agent performance report, then the response shows tickets resolved per agent in the period.

#### AC2 — Average handle time (spec AC-604)

Given agents have tickets with created and resolved timestamps, when the supervisor requests the report, then average handle time per agent is included.

## SQL tables

None — read-only query over existing tables.

```sql
SELECT a.userId AS agentId,
       u.displayName AS agentName,
       COUNT(t.id) AS ticketsResolved,
       AVG(DATEDIFF(MINUTE, t.createdAt, t.resolvedAt)) AS avgHandleMinutes
FROM Agents a
JOIN Users u ON a.userId = u.id
LEFT JOIN Tickets t ON t.assignedAgentId = a.id AND t.resolvedAt BETWEEN @startDate AND @endDate
WHERE a.departmentId = @departmentId
GROUP BY a.userId, u.displayName;
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-604 | Integration | `AgentPerformanceThroughput` | Given an agent resolved 15 tickets this month, when the supervisor requests the report, then that agent shows 15 tickets resolved. | Correct throughput count |
| TC-02 | AC-604 | Integration | `AgentPerformanceHandleTime` | Given an agent's tickets average 25 minutes handle time, when the supervisor requests the report, then avg handle time is 25 minutes. | Correct average handle time |

## Notes

Agent performance is scoped to the supervisor's department. Cross-department data is not returned.

## Open questions

None.

## Status evidence

Backend shipped: `GetAgentPerformanceReportQuery`, AC-153 tested end to end (create → assign →
Open → Resolved → counted). Frontend shipped 2026-08-27: `AgentPerformanceReportComponent`
(AC-162). See `docs/superpowers/plans/EPIC-08-US-606-feat-reporting/README.md`. Frontend **not yet
committed** — staged only, per explicit instruction this session.

Status is set from what is committed and executed, never from what is planned.
