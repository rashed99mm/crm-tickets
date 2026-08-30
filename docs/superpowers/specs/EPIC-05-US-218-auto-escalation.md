# US-218 — Auto-Escalation

## Problem
Automatic escalation currently stops at Level 1 instead of progressing through policy levels.

## Assumptions
- A1: Escalation levels and thresholds are configured in SLA policy data.
- A2: Every escalation is an immutable ticket-history event.

## Out of scope
Notification transport and auto-assignment rules.

## Acceptance Criteria
- AC-218.1: Given a breached target, then the ticket escalates to the configured next level.
- AC-218.2: Given a later breach, then escalation progresses until the configured terminal level.
- AC-218.3: Given repeated worker execution, then no duplicate transition occurs.

## Design
Use an explicit escalation state machine, policy lookup, idempotency key, and existing history model. Original story: `EPIC-05-US-218-auto-escalation.md` / AC-5.7.
