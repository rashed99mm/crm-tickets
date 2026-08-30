# US-204 — Inbound Email

## Problem
Inbound email cannot reliably create or append to tickets.

## Assumptions
- A1: The provider supplies a unique message ID and verifiable signature.
- A2: Ticket references are extracted only after signature verification.

## Out of scope
Provider selection/configuration and outbound email delivery.

## Acceptance Criteria
- AC-204.1: Given a verified email without a known reference, then one ticket and initial message are created.
- AC-204.2: Given a verified email with a known reference, then one message is appended.
- AC-204.3: Given the same provider message ID twice, then only one write occurs.
- AC-204.4: Given invalid signature or provider failure, then no unauthorized write occurs.

## Design
Use a signed webhook, unique provider-message key, transaction boundary, and `TicketMessage.Create`. Original story: `EPIC-03-US-204-inbound-email.md` / AC-3.2.
