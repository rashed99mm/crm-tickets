# US-118 · Every other transition is refused

| Field | Value |
|---|---|
| **Story** | `US-118` *(was `US-1.26`)* |
| **Epic** | [EPIC-02 Ticket management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-06` Ticket detail and lifecycle](../delivery-plan.md#feat-06--ticket-detail-and-lifecycle) |
| **Layer** | Backend |
| **Ships with** | [US-128](./US-128-ticket-detail-with-guarded-actions.md) *(frontend)* |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Support Manager |
| **Priority** | P0 |
| **Sprint** | [3 — Ticket detail, lifecycle, assignment and history](../delivery-plan.md#sprint-3--ticket-detail-lifecycle-assignment-and-history) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-2.8, FR-2.9, BR-3, BR-4 |
| **Spec criteria** | AC-38, AC-39 |
| **Depends on** | [US-016](./US-016-move-along-the-lifecycle.md) |

## Story

**As a support manager**, **I want** undefined status jumps refused, **so that** the lifecycle means something and the history is trustworthy.

## Business rules

- BR-3 — status changes only along the permitted transition table; other transitions refused as a
  state conflict (409), not a validation error (BRD).
- BR-4 — may not transition to the status already held (BRD).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Undefined transition refused as conflict (spec AC-38)

Given a transition not in the table — `New → Closed`, `Closed → Resolved`, `New → Resolved` — then
409 naming the rule. **Not 400**: the request is well-formed, the state is wrong.

#### AC2 — Self-transition refused (spec AC-39)

Given a transition to the status the ticket already holds, then 409.

## SQL tables

`Tickets.Status` again — the refusal happens in the entity before any write, so no column changes.
The string-persisted enum (see [S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#tickets))
is what keeps a refused transition from ever reaching the database as an integer.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-38 | Domain.Tests | PASS `TicketStatusTests.TicketStatus_RefusesEveryIllegalTransition` — 52 cases derived as the complement of 64 pairs minus 12 legal transitions | each transition not in the table / `TicketStatus.CanTransitionTo` / observe | refusal; state unchanged; nothing appended |
| TC-02 | AC-38 | Api.IntegrationTests | PASS `AC38_ChangeStatus_UndefinedTransition_Returns409NotValidationError` (3 named pairs) + `AC38_RefusedTransition_ChangesNothing` | `New → Closed`, `Closed → Resolved`, `New → Resolved` via API / attempt / inspect | **409** with code `ERR021`, not 400 |
| TC-03 | AC-39 | Domain.Tests | PASS covered by TC-01's complement, which includes all eight diagonal cells | a self-transition (`Open → Open`) / attempt / observe | refused like any undefined pair |
| TC-04 | AC-39 | Api.IntegrationTests | PASS `TicketLifecycleEndpointTests.AC39_ChangeStatus_ToTheStatusAlreadyHeld_Returns409` (3 cases). Code `TICKET_ALREADY_IN_STATUS` | self-transition via API / attempt / inspect | 409 with the named application code |

## Notes

`New → Closed` is deliberately impossible. Closing a request nobody opened means either the request was never real or the record is wrong, and both deserve a refusal rather than a silent state jump.

A public status setter would let any handler bypass the table, and eventually one would — which is why the setter is private and this is tested at the domain level with no database.

## Open questions

None.

## Status evidence

Implemented in `ChangeTicketStatusCommandHandler`, over the transition table the aggregate owns.

AC-38 -> `AC38_ChangeStatus_UndefinedTransition_Returns409NotValidationError` (the three pairs the
spec names) and `AC38_RefusedTransition_ChangesNothing`. AC-39 ->
`AC39_ChangeStatus_ToTheStatusAlreadyHeld_Returns409`. The complement of the whole table is covered
exhaustively at unit level by `TicketStatusTests.AC38_Refuses_Every_Transition_Outside_The_Table`.

**Divergence from AC-66:** the codes are `TICKET_TRANSITION_NOT_ALLOWED` and
`TICKET_ALREADY_IN_STATUS`, not `ERR021`/`ERR022`. The adopted platform uses named codes throughout;
this dates from the baseline adoption and belongs to FEAT-09's hardening pass.

Also proven: an unrecognised status value is a **400**, not a 409 -
`AC30_ChangeStatus_UnknownStatusValue_Returns400NotConflict`. That contrast is what AC-38 turns on.

Run 2026-08-26: 233 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
