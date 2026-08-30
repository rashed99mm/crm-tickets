# US-014 · A supervisor assigns work

| Field | Value |
|---|---|
| **Story** | `US-014` *(was `US-1.28`)* — rule proposal: *Assign Ticket to Agent* |
| **Epic** | [EPIC-02 Ticket management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-07` Assignment and authorization](../delivery-plan.md#feat-07--assignment-and-authorization) |
| **Layer** | Backend |
| **Ships with** | [US-128](./US-128-ticket-detail-with-guarded-actions.md) *(frontend)* |
| **Actor** | Team Lead |
| **Priority** | P0 |
| **Sprint** | [3 — Ticket detail, lifecycle, assignment and history](../delivery-plan.md#sprint-3--ticket-detail-lifecycle-assignment-and-history) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-2.12, BR-10 |
| **Spec criteria** | AC-42, AC-44 |
| **Depends on** | [US-009](./US-009-raise-a-ticket.md) *(sprint 2)*, [US-114](./US-114-role-permissions-refuse.md) *(sprint 1)* |

## Story

**As a supervisor**, **I want** to assign a ticket to an agent, **so that** it has an owner.

## Business rules

- BR-10 — only a supervisor assigns/reassigns, including to themselves (BRD).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Assignment changes the assignee (spec AC-42)

Given a supervisor, when assigning a ticket to an agent, then 200 and the assignee changes.

#### AC2 — Invalid target refused (spec AC-44)

Given a target user who does not exist or is not an agent, then 400.

## SQL tables

`Tickets.AssigneeId` — from the [S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#tickets):

```sql
[AssigneeId] NVARCHAR(450) NULL
    CONSTRAINT FK_Tickets_Assignee REFERENCES [dbo].[AspNetUsers] ([Id])
-- assignment also appends TicketHistory(ChangeType='Assigned'|'Reassigned')
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-42 | Api.IntegrationTests | PASS `AC42_Supervisor_AssignsUnassignedTicket_Returns200` | a supervisor / assign an unassigned ticket to an agent / inspect | 200; assignee changed on re-fetch |
| TC-02 | AC-42 | Api.IntegrationTests | PASS `AC42_Supervisor_ReassignsTicket_RecordsReassignedWithPreviousHolder` | a ticket already assigned / supervisor reassigns to another agent / inspect | 200; `Reassigned` history row |
| TC-03 | AC-44 | Api.IntegrationTests | PASS `AC44_Assign_UnknownTargetUser_Returns400KeyedToAssigneeId` | nonexistent user id in body / assign / inspect | 400 naming the field (`VAL010`) |
| TC-04 | AC-44 | Api.IntegrationTests | PASS `AC44_Assign_TargetIsNotAnAgent_Returns400` — a role lookup, not an existence check | a supervisor as target / assign / observe | 400 — not an agent |

## Notes

Assigning to a non-agent is a body-referenced lookup failure, so 400 rather than 404, consistent with US-009's third criterion. `SystemCode.VAL010 AssigneeInvalid` is already reserved for it.

## Open questions

None.

## Status evidence

Implemented as `AssignTicketCommand` and `POST /api/Tickets/{id}/assignee`, Supervisor policy.

AC-42 -> `AC42_Supervisor_AssignsUnassignedTicket_Returns200` and
`AC42_Supervisor_ReassignsTicket_RecordsReassignedWithPreviousHolder`. AC-44 ->
`AC44_Assign_UnknownTargetUser_Returns400KeyedToAssigneeId` and
`AC44_Assign_TargetIsNotAnAgent_Returns400`.

AC-44 needed a **role lookup, not an existence check**: a supervisor is a real user with a real id,
so `FindByIdAsync` alone would have accepted one as an assignee.

Role vocabulary resolved by [ADR-0012](../../adr/0012-seed-agent-and-supervisor-alongside-the-inherited-roles.md):
`Agent` and `Supervisor` are seeded alongside the platform's inherited six.

Run 2026-08-26: 233 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
