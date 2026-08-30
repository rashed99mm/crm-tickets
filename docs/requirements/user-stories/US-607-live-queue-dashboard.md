# US-607 · Live Queue Dashboard

| Field | Value |
|---|---|
| **Story** | `US-607` |
| **Epic** | [EPIC-08 Reports & Management](../epics/EPIC-08-reporting.md) |
| **Feature** | [`FEAT-19` Reporting](../delivery-plan.md#feat-19--reporting) |
| **Layer** | Backend / Frontend |
| **Ships with** | [US-601](./US-601-reports-controller.md) *(Backend)*, [US-610](./US-610-report-filter-ui.md) *(Frontend)* |
| **Actor** | Supervisor |
| **Priority** | P1 |
| **Sprint** | [13 — Reporting](../delivery-plan.md#sprint-13-reporting) · Slice S6 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-9.5 |
| **Spec criteria** | AC-607, DSH-2 |
| **Depends on** | [US-601](./US-601-reports-controller.md) *(Backend)*, [US-610](./US-610-report-filter-ui.md) *(Frontend)* |

## Story

**As a supervisor**, **I want** a live queue dashboard, **so that** I can see what tickets need immediate attention and rebalance workload in real time.

## Business rules

- No BRD BR-n covers this directly. The live queue shows unassigned tickets, tickets exceeding wait thresholds, and current agent load.
- BR-21: Report results are branch-scoped by default; the caller's branch is enforced via JWT claims.

## Acceptance criteria

#### AC1 — Live queue endpoint (spec AC-607)

Given the supervisor is authenticated, when they request the live queue, then the response lists unassigned tickets sorted by wait time.

#### AC2 — Agent load display (spec AC-607)

Given agents have active tickets, when the supervisor views the live queue, then agent names and current ticket counts are shown.

#### AC3 — Wait threshold alerts (spec AC-607)

Given tickets exceeding configurable wait thresholds, when the supervisor views the queue, then those tickets are flagged as urgent.

## SQL tables

None — read-only query over existing tables.

```sql
SELECT t.id, t.subject, t.priority, t.createdAt,
       DATEDIFF(MINUTE, t.createdAt, GETUTCDATE()) AS waitMinutes,
       CASE WHEN DATEDIFF(MINUTE, t.createdAt, GETUTCDATE()) > @waitThreshold THEN 1 ELSE 0 END AS isUrgent
FROM Tickets t
WHERE t.assignedAgentId IS NULL AND t.resolvedAt IS NULL
ORDER BY t.createdAt ASC;

SELECT a.userId AS agentId, u.displayName, COUNT(t.id) AS activeTickets
FROM Agents a
JOIN Users u ON a.userId = u.id
LEFT JOIN Tickets t ON t.assignedAgentId = a.id AND t.resolvedAt IS NULL
GROUP BY a.userId, u.displayName;
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-607 | Integration | `LiveQueueEndpointReturnsUnassigned` | Given 5 unassigned tickets, when the supervisor calls the live queue endpoint, then 5 tickets are returned sorted by wait time. | Correct unassigned ticket list |
| TC-02 | DSH-2 | E2E | `LiveQueueRendersAgentLoad` | Given agents with active tickets, when the supervisor opens the live queue, then agent names and ticket counts are visible. | Agent load panel renders |

## Notes

Frontend uses mockup `command-center.html` as reference for the live queue layout. Polling or SignalR for real-time updates is a design choice for implementation.

## Open questions

None.

## Status evidence

**Deliberately not built as specced** (spec addendum A4): needs a live-queue endpoint and an
agent-load view this codebase's `ReportsController` doesn't have, and the story's own SQL sketch
references `assignedAgentId`/an `Agents` table that don't match this schema's actual
`Ticket.AssigneeId`/`ApplicationUser` shape — building it would need its own spec, not an
adaptation of this one. Not built this pass. This story remains open.

Status is set from what is committed and executed, never from what is planned.
