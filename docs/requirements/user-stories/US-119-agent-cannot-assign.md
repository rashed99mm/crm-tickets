# US-119 · An agent cannot assign anything

| Field | Value |
|---|---|
| **Story** | `US-119` *(was `US-1.29`)* |
| **Epic** | [EPIC-02 Ticket management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-07` Assignment and authorization](../delivery-plan.md#feat-07--assignment-and-authorization) |
| **Layer** | Backend |
| **Ships with** | [US-128](./US-128-ticket-detail-with-guarded-actions.md) *(frontend)* |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Team Lead |
| **Priority** | P0 |
| **Sprint** | [3 — Ticket detail, lifecycle, assignment and history](../delivery-plan.md#sprint-3--ticket-detail-lifecycle-assignment-and-history) · Slice S1 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-2.12, BR-10 |
| **Spec criteria** | AC-43 |
| **Depends on** | [US-014](./US-014-supervisor-assigns-work.md) |

## Story

**As a supervisor**, **I want** assignment closed to agents entirely, **so that** work allocation stays a supervisory decision.

## Business rules

- BR-10 — only a supervisor assigns/reassigns, including to themselves (BRD).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Agents cannot assign anything (spec AC-43)

Given an agent, when assigning any ticket, then 403 — **including a ticket already assigned to
themselves**.

## SQL tables

None — this is authorization at the endpoint. The data it protects is `Tickets.AssigneeId`
(see [S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#tickets)); role membership
comes from `[AspNetUserRoles]`.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-43 | Api.IntegrationTests | PASS `AC43_Agent_AssigningAnyTicket_Returns403` | an agent token / assign any ticket / observe | 403, code `ERR023` |
| TC-02 | AC-43 (the parenthetical) | Api.IntegrationTests | PASS `AC43_Agent_AssigningTheirOwnTicket_StillReturns403` — permission precedes ownership | an agent token / assign a ticket **already assigned to themselves** / observe | still 403 — permission precedes ownership |

## Notes

The parenthetical is the interesting half. Self-assignment feels harmless and is the case an implementation is most likely to allow by accident, because the obvious guard is "is this my ticket" rather than "am I permitted to assign at all".

## Open questions

None.

## Status evidence

Enforced by `[Authorize(Policy = "Supervisor")]` on the assign endpoint - an endpoint-level control,
because "an agent may not assign" does not depend on which ticket was addressed.

AC-43 -> `AC43_Agent_AssigningAnyTicket_Returns403` and
`AC43_Agent_AssigningTheirOwnTicket_StillReturns403`.

The second is the parenthetical and the point of the story: a "reasonable" ownership shortcut would
permit it and would read as defensible in review. Permission precedes ownership - assignment is a
supervisory act regardless of who currently holds the ticket.

Run 2026-08-26: 233 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
