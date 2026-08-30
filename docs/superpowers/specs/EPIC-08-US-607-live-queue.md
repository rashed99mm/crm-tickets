# US-607 — Live Queue Dashboard

## Problem
Managers cannot see current queue age, agent load, or wait-threshold risk.

## Assumptions
- A1: Refresh uses the existing realtime abstraction or cancellable polling.
- A2: Scope uses US-608 rules.

## Out of scope
Automatic assignment itself.

## Acceptance Criteria
- AC-607.1: Given queue data, then current waiting tickets and age are displayed.
- AC-607.2: Agent load is displayed for the selected scope.
- AC-607.3: Tickets over the configured wait threshold are visibly flagged.

## Design
Create a read-only queue projection and management screen with stale/loading/error states. Original story: `EPIC-08-US-607-live-queue-dashboard.md` / AC-607, DSH-2.
