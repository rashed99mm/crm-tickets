# US-805 — Permission Administration UI

## Problem
Administrators cannot manage role permissions safely.

## Assumptions
- A1: US-804 policy enforcement is complete before this UI ships.
- A2: The last required permission cannot be removed.

## Out of scope
Permission model/schema creation.

## Acceptance Criteria
- AC-805.1: Authorized administrators can list permissions and mappings.
- AC-805.2: Authorized administrators can assign a permission to a role.
- AC-805.3: Authorized administrators can revoke a permission from a role.
- AC-805.4: Removing all required permissions is rejected.

## Design
Add authorized API handlers and an Angular management screen with translated loading/error/success states. Original story: `EPIC-09-US-805-permission-admin-ui.md` / AC-805.
