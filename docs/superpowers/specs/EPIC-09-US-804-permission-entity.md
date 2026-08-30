# US-804 — Permission Entity and Role Mapping

## Problem
Roles cannot be assigned stable, fine-grained permissions.

## Assumptions
- A1: Permission keys are stable identifiers, not display labels.
- A2: Seed operations are idempotent.

## Out of scope
The administration screen, which is US-805.

## Acceptance Criteria
- AC-804.1: Permission keys can be stored and uniquely identified.
- AC-804.2: Roles can be mapped to permissions.
- AC-804.3: Required permissions are seeded idempotently.

## Design
Add permission and role-permission entities, EF configuration, migration, seed, and server-side policy evaluation. Original story: `EPIC-09-US-804-permission-entity.md` / AC-804.
