# US-202 — Message Timeline

## Problem
Recorded ticket messages are not fully evidenced in the ticket-detail UI.

## Assumptions
- A1: `TicketMessage` and `GET /api/Tickets/{id}/messages` remain the source of truth.

## Out of scope
Email ingestion and outbound delivery; those are US-203–205.

## Acceptance Criteria
- AC-202.1: Given messages, when ticket detail loads, then it renders oldest-first with direction, channel, sender, body, and time.
- AC-202.2: Given no messages, then a distinct translated empty state renders.

## Design
Use `AsyncState`, `TicketApi.listMessages`, shared date/localization components, and no agent-only mutation controls. Original story: `EPIC-03-US-202-message-timeline.md` / AC-3.4.
