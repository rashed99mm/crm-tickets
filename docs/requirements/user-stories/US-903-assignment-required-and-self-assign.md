# US-903 · Assignment Required Before Work; Self-Assign

| Field | Value |
|---|---|
| **Story** | `US-903` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-28`](../delivery-plan.md#feat-28) |
| **Layer** | Backend / Frontend |
| **Ships with** | [US-912](./US-912-ticket-queue-redesign.md) *(Frontend)* |
| **Actor** | Agent |
| **Priority** | P0 |
| **Sprint** | 17 — Phase 2 workflow |
| **Estimate** | 3 points |
| **Status** | `not started` |

## Story

**As an agent**, **I want** to be able to take work from the queue (self-assign) when no supervisor is
around, **so that** work isn't blocked waiting for a manager, while the workflow still refuses to
start work on a ticket nobody owns.

## Business rules

- Transitions into `In Progress` and the waiting states are refused when `AssigneeId` is null.
  `ChangeStatus` already knows the loaded aggregate, so this is an aggregate guard, not a
  controller check.
- An agent may self-assign from the queue or detail screen without supervisor action, matching the
  workflow's manual-assignment alternative. Supervisor/Admin retain the existing assign endpoint.

## Acceptance criteria

#### AC1 — Work states require an assignee

Given a ticket with no `AssigneeId`, when moved to `In Progress`, `Waiting for Customer` or `Waiting
for Internal Team`, then `ChangeStatus` throws; given an assignee, the transition is allowed.

#### AC2 — Agent self-assigns

Given a logged-in Agent viewing the queue or a detail screen, then a self-assign control exists; when
used, `POST /api/tickets/{id}/assignee` is called with the agent's own id and the ticket's assignee
updates.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Unit | `Ticket_CannotEnterWorkWithoutAssignee` | throws on `In Progress` + waiting states when unassigned |
| TC-02 | AC1 | Unit | `Ticket_CanEnterWork_WhenAssigned` | allowed |
| TC-03 | AC2 | Integration | `Agent_SelfAssigns_FromQueue` | 200, assignee = agent |

## SQL tables

None.

## Notes

Extends the assign endpoint's authorization from Supervisor-only to include the agent's own id (the
existing `AC-119` rule "agent cannot assign others" — strengthened to "agent may assign self").
Details in the FEAT-28 plan's assign task.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.