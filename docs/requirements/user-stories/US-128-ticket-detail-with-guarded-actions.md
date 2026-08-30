# US-128 · Ticket detail shows the story and hides what I cannot do

| Field | Value |
|---|---|
| **Story** | `US-128` *(was `US-1.40`)* |
| **Epic** | [EPIC-04 Agent dashboard](../epics/EPIC-04-agent-dashboard.md) |
| **Feature** | [`FEAT-06` Ticket detail and lifecycle](../delivery-plan.md#feat-06--ticket-detail-and-lifecycle) |
| **Layer** | Frontend |
| **Ships with** | [US-010](./US-010-ticket-detail.md) *(backend)*, [US-016](./US-016-move-along-the-lifecycle.md) *(backend)*, [US-118](./US-118-refuse-undefined-transitions.md) *(backend)*, [US-026](./US-026-reopen-and-refuse-lost-updates.md) *(backend)*, [US-014](./US-014-supervisor-assigns-work.md) *(backend)*, [US-119](./US-119-agent-cannot-assign.md) *(backend)*, [US-120](./US-120-status-change-belongs-to-assignee.md) *(backend)*, [US-121](./US-121-every-change-recorded-immutably.md) *(backend)*, [US-022](./US-022-read-ticket-history.md) *(backend)* |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [3 — Ticket detail, lifecycle, assignment and history](../delivery-plan.md#sprint-3--ticket-detail-lifecycle-assignment-and-history) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-4.2, FR-4.4, FR-4.5 |
| **Spec criteria** | AC-61 |
| **Depends on** | [US-010](./US-010-ticket-detail.md) *(sprint 2)*, [US-120](./US-120-status-change-belongs-to-assignee.md) *(sprint 3)* |

## Story

**As an agent**, **I want** the customer summary, the history timeline and only the actions available to me, **so that** the screen matches my permissions.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- The detail view shows customer summary, history timeline and the status action; the assign action
  is hidden for agents and refused by the server if called anyway (from AC-61).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Ticket detail with guarded actions (spec AC-61)

Given ticket detail, then customer summary, history timeline and the status action are shown; the
assign action is hidden for agents **and refused by the server if called anyway**.

## SQL tables

None — frontend story. Reads the ticket detail payload (`Tickets` + `TicketHistory` +
customer summary, [S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#tickethistory)).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-61 | Frontend (Vitest) | PASS `AC61: renders the customer summary, the history timeline and the status action` | flush a detail response / inspect DOM | customer summary, history timeline, status action all rendered |
| TC-02 | AC-61 (hidden half) | Frontend (Vitest) | PASS `AC61: the assign action is hidden for an agent` | session role is `Agent` / inspect actions | assign action **not rendered** |
| TC-03 | AC-61 (refused half — the security control) | Api.IntegrationTests | PASS `AC43_Agent_AssigningAnyTicket_Returns403` and `AC43_Agent_AssigningTheirOwnTicket_StillReturns403` (FEAT-07) — **this is the control; TC-02 is the courtesy** | an `Agent` token calls assign anyway / observe | 403 from the server regardless of UI |
| TC-04 | AC-61 | E2E (Playwright) | **not covered** — the Playwright pass belongs to AC-64 / FEAT-11 in Phase 4, and is not claimed by this story | agent signs in, opens a ticket / observe | same visibility rules in a real browser |

## Notes

Both halves are required and they are different kinds of thing. Hiding the button is a usability measure; the server refusal is the security control. A reviewer who only sees the hidden button has been shown nothing about whether the system is safe.

## Open questions

None.

## Status evidence

Implemented as `admin-app/features/tickets/ticket-detail.component.ts`, route `tickets/:id`.

AC-61 -> seven tests naming it, covering the customer summary, the history timeline, the status
action offering only permitted transitions, the hidden assign action for an agent, the rowVersion
echo on both mutations, and the 409 path.

**Which half is the control:** the criterion says the assign action is hidden *and refused by the
server if called anyway*. Hiding it (`AC61: the assign action is hidden for an agent`) is a
courtesy. The control is `AC43_Agent_AssigningAnyTicket_Returns403` in the backend suite, and it
holds whatever the browser renders. TC-03 records that split.

**Backend gap this story uncovered:** the assign picker had no endpoint to call - `/api/Users` is
Admin-only and a supervisor is not an administrator. `GET /api/Tickets/assignable-agents` was added.
Second time a backend plan failed to derive an endpoint from its frontend counterpart.

**Not claimed here:** TC-04, the Playwright pass, belongs to AC-64 / FEAT-11 in Phase 4.

Run 2026-08-26: `npx ng test admin-app --watch=false` - 49 passed, 0 failed;
`npx ng test common --watch=false` - 55 passed; `npx ng build admin-app` clean.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
