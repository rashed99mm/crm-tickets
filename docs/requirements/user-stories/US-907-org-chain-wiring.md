# US-907 · Org-Chain Wiring (Dormant Columns Populated)

| Field | Value |
|---|---|
| **Story** | `US-907` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-28`](../delivery-plan.md#feat-28) |
| **Layer** | Backend |
| **Ships with** | (none — enabler for US-905/910) |
| **Actor** | — |
| **Priority** | P1 |
| **Sprint** | 17 — Phase 2 workflow |
| **Estimate** | 3 points |
| **Status** | `not started` |

## Story

**As a planner**, **I want** ticket and user organisational columns actually populated, **so that**
the BI hierarchy traverses non-null data instead of a schema that is always NULL for every row.

## Business rules

- Ticket inherits `DepartmentId`/`BranchId`/`TeamId` from its assignee on assign; at creation it
  inherits the acting agent's values when the agent has them.
- `ApplicationUser.DepartmentId`/`BranchId` were already nullable and unused; nothing new to migrate,
  only the write-back.

## Acceptance criteria

#### AC1 — Inherited on assign

Given an agent with department/branch/team creates or gets assigned a ticket, then the ticket's
`DepartmentId`/`BranchId`/`TeamId` reflect the actor's/assignee's values.

#### AC2 — Hierarchy traversable

Given wired users/tickets, then Org→Branch→Dept→Team→Agent→Ticket is traversable from non-null FKs.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Unit | `Ticket_Assign_PropagatesOrg` | detail reflects assignee org |
| TC-02 | AC1 | Integration | `CreateTicket_InheritsActingAgentOrg` | ticket carries agent org |

## SQL tables

None new — write-back to existing `Tickets.DepartmentId/BranchId` + new `Tickets.TeamId`.

## Notes

Wiring happens on assignment and on creation, reading the acting agent's organisation values. See
FEAT-28 plan.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.