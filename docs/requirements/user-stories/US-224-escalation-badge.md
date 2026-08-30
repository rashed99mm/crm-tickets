# US-224 · Escalation Badge on Ticket Queue

| Field | Value |
|---|---|
| **Story** | `US-224` |
| **Epic** | [EPIC-02 Ticket Management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Frontend |
| **Ships with** | [US-218](./EPIC-05-US-218-auto-escalation.md), [US-225](./US-225-ticket-escalation-state.md) *(frontend)* |
| **Actor** | Support Agent |
| **Priority** | P1 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-5.7 |
| **Spec criteria** | AC-5.7 |
| **Depends on** | [US-225](./US-225-ticket-escalation-state.md), [US-011](./US-011-ticket-detail-screen.md) |

## Story

**As a support agent**, **I want** to see an escalation status badge on tickets in the queue, **so that** I can prioritise escalated tickets correctly.

## Business rules

- BR-30 — Escalation badge is displayed prominently on each ticket row in the queue view (BRD).

## Acceptance criteria

#### AC1 — Escalation Badge on Queue (spec AC-5.7)

Given the ticket queue view, when a ticket has an active escalation, then an escalation badge is displayed on the ticket row indicating the escalation level.

#### AC2 — Sort by Escalation (spec AC-5.7)

Given the ticket queue view, when the user sorts by escalation, then escalated tickets appear at the top of the list.

## SQL tables

None — frontend story, reads `EscalationState` from API response.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5.7 | E2E | `TicketQueue_ShouldShowEscalationBadge` | Given a ticket with an active escalation state, when the queue is loaded, then an escalation badge is visible on the ticket row | Escalation badge displayed |
| TC-02 | AC-5.7 | Unit | `EscalationBadge_ShouldShowLevel` | Given a ticket at escalation level 2, when the badge is rendered, then level 2 is indicated on the badge | Badge shows "Level 2" |
| TC-03 | AC-5.7 | Unit | `TicketQueue_ShouldSortByEscalation` | Given tickets with and without escalation, when sorted by escalation, then escalated tickets appear first | Escalated tickets at top |

## Notes

Badge uses colour coding: amber for level 1, red for level 2+, dark red for level 3+. Extends the existing ticket queue component with an escalation column.

## Open questions

None.

## Status evidence

Shipped 2026-08-27 — escalation badge and a client-side "sort by escalation" toggle on
`TicketQueueComponent`, backed by a new `EscalationState` projection on `TicketListItemDto`
(`GetTicketsQueryHandler`). Sort is client-side over the loaded page only, not a second
server-side sort dimension (recorded as addendum assumption A7). Backend:
`dotnet test --filter "FullyQualifiedName~AC158_GetTickets_ExposesEscalationState"` → 2/2 passing.
Frontend: `npx ng test admin-app --watch=false --include='**/ticket-queue.component.spec.ts'` →
passing (part of a combined 22/22 run). See
`docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-escalation/README.md`'s "Frontend addendum".
**Not yet committed** — staged only, per explicit instruction this session.

Status is set from what is committed and executed, never from what is planned.
