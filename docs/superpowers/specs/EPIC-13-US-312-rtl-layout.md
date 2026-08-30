# US-312 — RTL Layout

## Problem
Arabic users need the same workflows mirrored correctly.

## Assumptions
- A1: `LocaleStore` controls document language and direction.

## Out of scope
New Arabic copy review, which is US-313.

## Acceptance Criteria
- AC-312.1: Layout containers, spacing, forms, and tables mirror under RTL.
- AC-312.2: Directional icons and pagination cues mirror; non-directional icons do not.

## Design
Use logical CSS utilities, `dir="rtl"`, existing RTL safety tests, and Arabic viewport checks. Original story: `EPIC-13-US-312-rtl-layout.md` / AC-23.
