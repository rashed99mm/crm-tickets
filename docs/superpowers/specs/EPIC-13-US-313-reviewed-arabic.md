# US-313 — Reviewed Arabic

## Problem
Placeholder or missing Arabic copy undermines a bilingual product.

## Assumptions
- A1: Product review approves the Arabic catalogue before the story is marked done.

## Out of scope
Machine translation claims and locale-specific business data.

## Acceptance Criteria
- AC-313.1: Every visible key used by both apps has reviewed Arabic copy.
- AC-313.2: No key silently falls back to its identifier.

## Design
Inventory translation keys, replace placeholders, and test language switching without refetching. Original story: `EPIC-13-US-313-reviewed-arabic.md` / AC-24.
