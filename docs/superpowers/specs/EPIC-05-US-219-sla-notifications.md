# US-219 — SLA Notifications

## Problem
Warnings and breaches do not notify the correct recipients reliably.

## Assumptions
- A1: Recipients derive from assignee, supervisor, and escalation level.
- A2: Notification events are deduplicated by ticket, target, and event type.

## Out of scope
New notification channels beyond the existing messaging infrastructure.

## Acceptance Criteria
- AC-219.1: Given a breach, then configured recipients receive one breach notification.
- AC-219.2: Given an imminent breach, then configured recipients receive one warning notification.
- AC-219.3: Given publisher failure, then retry behavior is bounded and observable.

## Design
Add versioned notification contracts and consumers; never log message bodies or credentials. Original story: `EPIC-05-US-219-sla-notifications.md` / AC-5.8.
