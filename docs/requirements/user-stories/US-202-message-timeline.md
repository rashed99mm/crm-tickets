# US-202 · Message timeline on ticket detail

| Field | Value |
|---|---|
| **Story** | `US-202` |
| **Epic** | [EPIC-03 Communication channels](../epics/EPIC-03-communication-channels.md) |
| **Feature** | [`FEAT-12` Customer notes](../delivery-plan.md#feat-12--customer-notes) |
| **Layer** | Frontend |
| **Ships with** | [US-201](./US-201-record-message.md) *(backend)* |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [6 — Conversation record](../delivery-plan.md#sprint-6--conversation-record) · Slice S5 |
| **Estimate** | 3 points |
| **Status** | `done` — AC-202.1/AC-202.2 covered by AC-named component tests (see Status evidence) |
| **BRD requirements** | FR-3.4 |
| **Spec criteria** | AC-3.4 |
| **Depends on** | [US-201](./US-201-record-message.md) |

## Story

**As a support agent**, **I want** to see all messages on the ticket detail, **so that** I understand the full communication history.

## Business rules

- BR-2 — Append-only history: messages are presented in chronological order and cannot be reordered or hidden (BRD).

## Acceptance criteria

#### AC1 — Display message timeline on ticket detail (spec AC-3.4)

Given a ticket detail view, when messages exist, then a timeline shows all messages ordered by `SentAt` ascending with a visible direction indicator (Inbound/Outbound) and sender information.

#### AC2 — Empty state

Given a ticket with no messages, when the ticket detail is viewed, then an empty state message is displayed indicating no communication history.

## SQL tables

No additional tables. Reads from `TicketMessages` created by US-201.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-3.4 | Component | `MessageTimeline_DisplaysMessagesInOrder` | Given a ticket with 3 messages at different times, when the ticket detail is rendered, then messages appear in ascending SentAt order | Messages ordered oldest-first with direction indicator visible |
| TC-02 | AC-3.4 | Component | `MessageTimeline_ShowsSenderAndDirection` | Given an inbound and outbound message, when the timeline renders, then each message shows sender name and direction badge | Inbound shows customer name, outbound shows agent name |
| TC-03 | AC-3.4 | Component | `MessageTimeline_EmptyState` | Given a ticket with zero messages, when the detail view loads, then empty state text is shown | "No messages yet" or equivalent displayed |
| TC-04 | AC-3.4 | E2E | `TicketDetail_MessageTimelineFlow` | Given a new ticket, when agent sends a reply, then the message appears in the timeline | New message visible in timeline without page refresh |

## Notes

- Timeline is a child component of the ticket detail page from US-010.
- Uses the generated typed API client to call the backend message endpoint.
- Consider virtual scrolling if message count grows large; not required for MVP.

## Open questions

None.

## Status evidence

Implemented as `TicketMessagesComponent`, wired into `ticket-detail.component.html` beside the
status-history timeline.

Added AC-named component tests (TDD red→green) on 2026-08-27 in
`frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.spec.ts`:
`US202_MessageTimeline_RendersOldestFirstWithDirectionChannelSenderBodyAndTime`,
`US202_MessageTimeline_RendersDistinctEmptyState`,
`US202_MessageTimeline_RendersLoadFailureInsteadOfEmptyState`, and
`US202_MessageTimeline_UsesTicketApiListMessages`. The parent spec gained
`US202_TicketDetail_RendersMessageTimelineForLoadedTicket`
(`ticket-detail.component.spec.ts`).

The first (red) run of the channel/order test proved a real production gap: the template rendered
direction, sender, time and body but **not** the per-message channel (violating AC-202.1's
"direction, channel, sender, body, and time"). Fixed in `ticket-messages.component.html` by adding
a localized channel badge (`messages.channel.system` → "In-app", `messages.channel.email` →
"Email"), preserving oldest-first order, sender provenance, escaped body rendering and
error-versus-empty semantics.

Execution evidence:
- `npx ng test admin-app --watch=false --include "**/ticket-messages.component.spec.ts"` → **4 passed** (1 file).
- `npx ng test admin-app --watch=false --include "**/ticket-detail.component.spec.ts"` → **10 passed** (1 file).
- `npx ng build admin-app` → clean, 0 errors.
- `npx ng test admin-app --watch=false` → 139/140 passing. The single failure is **pre-existing and
  unrelated** to US-202: `nav-routes.spec.ts` (`every top-level screen route is offered by the
  sidebar`) flags `/reports/sla-performance` and `/reports/agent-performance` as not offered by the
  sidebar — a reporting feature (US-6xx) gap, not a ticket-message defect.

E2E `TicketDetail_MessageTimelineFlow` (TC-04) is not implemented here.

Status is set from what is committed and executed, never from what is planned.
