# US-220 — Auto-Assignment

## Problem
New tickets remain unassigned despite configured assignment rules.

## Assumptions
- A1: Only active, eligible agents participate.
- A2: Manual assignment always wins over automation.

## Out of scope
AI assignment and cross-branch assignment before US-306 is resolved.

## Acceptance Criteria
- AC-220.1: Given a new eligible ticket, then the configured rule assigns it.
- AC-220.2: Round-robin order is deterministic and persists across worker restarts.
- AC-220.3: Load-based assignment chooses the least-loaded eligible agent with deterministic ties.

## Design
Use an Application assignment-policy port, transactional assignment, and one history event. Original story: `EPIC-05-US-220-auto-assignment.md` / AC-5.6.
