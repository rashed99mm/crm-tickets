# US-217 — SLA Pre-Breach Warning

## Problem
Agents receive no warning before an SLA target is breached.

## Assumptions
- A1: The warning threshold is configurable per SLA policy.
- A2: Warning delivery is idempotent per ticket and target.

## Out of scope
Business-hours calculation itself and automatic assignment.

## Acceptance Criteria
- AC-217.1: Given an active ticket approaching its target, then one warning event is generated.
- AC-217.2: Given a paused/resolved ticket or repeated worker run, then no invalid duplicate warning is generated.

## Design
The SLA worker uses business time, writes an idempotency record/event, and publishes through the existing messaging abstraction. Original story: `EPIC-05-US-217-sla-warning.md` / AC-5.9.
