# US-120 · Status changes belong to the ticket's own assignee

| Field | Value |
|---|---|
| **Story** | `US-120` *(was `US-1.30`)* |
| **Epic** | [EPIC-02 Ticket management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-07` Assignment and authorization](../delivery-plan.md#feat-07--assignment-and-authorization) |
| **Layer** | Backend |
| **Ships with** | [US-128](./US-128-ticket-detail-with-guarded-actions.md) *(frontend)* |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Support Manager |
| **Priority** | P0 |
| **Sprint** | [3 — Ticket detail, lifecycle, assignment and history](../delivery-plan.md#sprint-3--ticket-detail-lifecycle-assignment-and-history) · Slice S1 |
| **Estimate** | 8 points |
| **Status** | `done` |
| **BRD requirements** | FR-10.6, BR-11 |
| **Spec criteria** | AC-45, AC-46, AC-47 |
| **Depends on** | [US-016](./US-016-move-along-the-lifecycle.md), [US-014](./US-014-supervisor-assigns-work.md) |

## Story

**As a support manager**, **I want** an agent able to progress only their own tickets, **so that** one agent cannot alter another's work.

## Business rules

- BR-11 — agent changes status only of own tickets, supervisor changes any (BRD).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Other agent's ticket forbidden (spec AC-45)

Given an agent, when changing the status of a ticket **not** assigned to them, then 403 and the
ticket is unchanged.

#### AC2 — Own ticket permitted (spec AC-46)

Given an agent, when changing the status of their own assigned ticket, then 200.

#### AC3 — Supervisor overrides ownership (spec AC-47)

Given a supervisor, when changing the status of any ticket, then 200.

## SQL tables

`Tickets.AssigneeId` read by the handler — from the
[S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#tickets). No column changes here;
the story is about who may write.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-45 | Api.IntegrationTests | PASS `AC45_Agent_ChangingAnotherAgentsTicket_Returns403AndTicketUnchanged` — re-fetches to prove the refusal was total | an agent / change status of a ticket assigned to **another** agent / observe | 403 code `ERR023` **and** the ticket's status unchanged on re-fetch |
| TC-02 | AC-46 | Api.IntegrationTests | PASS `AC46_Agent_ChangingTheirOwnTicket_Returns200` | the assignee / change their own ticket / observe | 200; transition applies |
| TC-03 | AC-47 | Api.IntegrationTests | PASS `AC47_Supervisor_ChangingAnyTicket_Returns200` | a supervisor / change any ticket (assigned to someone else) / observe | 200 — supervisor overrides ownership |
| TC-04 | AC-45/46 (unit half) | Application.Tests | **superseded by an integration test** — `AC45_Agent_ChangingAnUnassignedTicket_Returns403` plus TC-01's re-fetch cover the refusal branch against a real database. A faked-repository unit test would assert the same branch with less fidelity; not written | handler with a faked ticket repo: other agent's ticket vs own / execute / inspect result + entity | refusal branch leaves the entity untouched |

## Notes

This is the security showcase of the slice, and endpoint-level authorization cannot satisfy it. Only the handler has loaded the ticket and can see who it is assigned to; a route-level role check cannot distinguish the first criterion from the second, because both are the same role calling the same endpoint.

The first criterion asserts two things — the refusal *and* that the ticket is unchanged. A handler that mutates then checks would pass a status-code-only assertion.

## Open questions

None.

## Status evidence

Enforced **inside** `ChangeTicketStatusCommandHandler`, over `Ticket.IsAssignedTo`.

AC-45 -> `AC45_Agent_ChangingAnotherAgentsTicket_Returns403AndTicketUnchanged` and
`AC45_Agent_ChangingAnUnassignedTicket_Returns403`. AC-46 ->
`AC46_Agent_ChangingTheirOwnTicket_Returns200`. AC-47 ->
`AC47_Supervisor_ChangingAnyTicket_Returns200`.

**No endpoint policy can satisfy this.** AC-45 and AC-46 differ only by which ticket is addressed:
same caller, same role, same endpoint, same verb, and one is 200 while the other is 403. That is not
knowable until the ticket is loaded, which is after the policy has run.

The unassigned-ticket case is not in this story's test cases and was added deliberately: an
implementation that inverted the check, or read null as "anyone", would pass the other three and
hand every agent every unassigned ticket.

Run 2026-08-26: 233 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
