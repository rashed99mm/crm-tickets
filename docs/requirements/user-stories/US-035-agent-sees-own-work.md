# US-035 · An agent sees only their own work

| Field | Value |
|---|---|
| **Story** | `US-035` *(was `US-1.23`)* — rule proposal: *View Assigned Tickets* |
| **Epic** | [EPIC-04 Agent dashboard](../epics/EPIC-04-agent-dashboard.md) |
| **Feature** | [`FEAT-05` Ticket queue](../delivery-plan.md#feat-05--ticket-queue) |
| **Layer** | Backend |
| **Ships with** | [US-038](./US-038-usable-ticket-list.md) *(frontend)*, [US-126](./US-126-empty-never-looks-like-failure.md) *(frontend)* |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [2 — Customers, ticket capture and queue](../delivery-plan.md#sprint-2--customers-ticket-capture-and-queue) · Slice S1 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-2.6, FR-4.1 |
| **Spec criteria** | AC-34 |
| **Depends on** | [US-013](./US-013-filter-the-queue.md) |

## Story

**As an agent**, **I want** the list filtered to my assignments, **so that** I can work my queue without scanning everyone else's.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- The "mine" filter is derived from the caller's token, never from a client-supplied assignee id
  (from AC-34).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Mine filter from token (spec AC-34)

Given the caller is an agent, when listing with the "mine" filter, then only tickets assigned to
that caller.

## SQL tables

`Tickets.AssigneeId` — from the [S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#tickets):

```sql
[AssigneeId] NVARCHAR(450) NULL
    CONSTRAINT FK_Tickets_Assignee REFERENCES [dbo].[AspNetUsers] ([Id]),
CREATE INDEX IX_Tickets_AssigneeId ON [dbo].[Tickets] ([AssigneeId]);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-34 | Api.IntegrationTests | PASS `AC34_GetTickets_MineReturnsOnlyTicketsAssignedToTheCaller` — closed 2026-08-26 once FEAT-07 could assign | an agent with assigned and unassigned tickets / list with `mine=true` / inspect items | only tickets assigned to that caller |
| TC-02 | AC-34 (security) | Api.IntegrationTests | PASS `AC34_GetTickets_MineIgnoresSuppliedAssigneeId` — the id comes from the token, so a supplied `assigneeId` cannot widen the result | `mine=true` from two different agents' tokens / compare results | each sees their own — derived from token, never a client-supplied id |

## Notes

"Mine" is derived from the token, not from a client-supplied assignee id. Accepting the id from the request would make this filter a way to read another agent's queue.

## Open questions

None.

## Status evidence

Implemented in Day 1 as the `mine` flag on `GetTicketsQuery`; **completed 2026-08-26** once
assignment existed.

AC-34 -> `AC34_GetTickets_MineReturnsOnlyTicketsAssignedToTheCaller` (the positive half, previously
untestable), `AC34_GetTickets_MineIgnoresSuppliedAssigneeId` (the security half) and
`AC34_GetTickets_MineWithNoTickets_Returns200EmptyPage`.

This story stood at `partial` through Day 1 because nothing could assign a ticket, so no fixture
could put one in the caller's queue. The code was correct throughout; what was missing was the
fixture, and FEAT-07 supplied it.

Run 2026-08-26: 233 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
