# US-913 · Ticket Detail UX: Guided Transitions, Conversation-First

| Field | Value |
|---|---|
| **Story** | `US-913` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-30`](../delivery-plan.md#feat-30) |
| **Layer** | Frontend |
| **Ships with** | [US-901](./US-901-real-life-8-state-lifecycle.md) *(Backend)* |
| **Actor** | Agent |
| **Priority** | P0 |
| **Sprint** | 19 — UX redesign |
| **Estimate** | 5 points |
| **Status** | `not started` |

## Story

**As an agent**, **I want** status moves I can see and confirm, and the conversation where my eye
already is, **so that** I never change a ticket silently and never hunt for the thread.

## Business rules

- Status actions are explicit buttons/confirmed actions shown for the current status's legal
  transitions (from the shared status model), with a success/failure toast; a 409 triggers a
  reload, not a silent loss.
- Messages section is conversation-first: raised directly under the header, oldest-first, composer
  anchored below, clear inbound/outbound visual direction. Replaces the buried message-card placement.
- All strings translated, RTL-safe.

## Acceptance criteria

#### AC1 — Confirmed transitions

Given a ticket detail, then the current status is shown, legal transitions render as confirmed actions
(never a silent `<select>`), and the outcome is toasted.

#### AC2 — Conversation-first layout

Given the detail screen, then the messages thread sits directly under the header with the composer
anchored below.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Component | `TicketDetail_StatusMove_Confirms_Toasts` | confirm dialog + toast on success |
| TC-02 | AC1 | Component | `TicketDetail_409_Reloads` | reload preserves state |
| TC-03 | AC2 | Component | `TicketDetail_MessagesBeneathHeader` | composer + thread layout |

## SQL tables

None.

## Notes

Reworks the ticket-detail and message-thread screens; the transition table moves to the shared status
model (US-919 or here).

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.