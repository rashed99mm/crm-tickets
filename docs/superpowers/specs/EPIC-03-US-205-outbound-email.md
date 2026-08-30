# US-205 — Outbound Email

## Problem
An agent cannot send a ticket reply through email while preserving the ticket record.

## Assumptions
- A1: Existing record-message authorization remains authoritative.
- A2: The ticket reference is always included in the subject.

## Out of scope
Inbound email ingestion and provider configuration.

## Acceptance Criteria
- AC-205.1: Given a valid reply, then email is sent and the outbound message is recorded.
- AC-205.2: Given any reply, then its subject contains the ticket reference.
- AC-205.3: Given provider failure, then the UI shows a safe error and does not claim delivery.

## Design
Add an email channel to the existing ticket-detail composer and use `IEmailSender`. Original story: `EPIC-03-US-205-outbound-email.md` / AC-3.3.
