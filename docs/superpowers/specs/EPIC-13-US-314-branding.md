# US-314 — Organisation Branding

## Problem
Each organisation cannot safely apply its approved brand identity.

## Assumptions
- A1: Branding is tenant-scoped and public branding fields are safe to expose.
- A2: Missing branding uses the current default tokens.

## Out of scope
Arbitrary executable theme code or user-uploaded scripts.

## Acceptance Criteria
- AC-314.1: Authorized administrators can store and retrieve valid branding settings.
- AC-314.2: Invalid colors, URLs, and assets are rejected.
- AC-314.3: Frontend applies branding with safe defaults and tenant isolation.

## Design
Use validated settings/API contracts, CSS custom properties, safe asset URLs, and a centralized brand store. Original story: `EPIC-13-US-314-branding.md` / AC-25.
