# US-129 — End-to-End Journey

## Problem
The full sign-in-to-persistence workflow is not proven in a real browser.

## Assumptions
- A1: The seeded supervisor account is available in the test environment.
- A2: The existing Playwright journey is the single terminal S1 journey.

## Out of scope
Additional per-feature E2E journeys.

## Acceptance Criteria
- AC-129.1: Given a clean database, when the browser signs in, creates a customer and ticket, assigns it, changes status, and reloads, then the status and history remain visible.
- AC-129.2: Given any failed step, when the journey finishes, then the failure identifies the real endpoint or selector and is not hidden by a fallback.

## Design
Use `frontend/e2e/journey.spec.ts`, stable labels/test IDs, the real InternalApi, and the existing LocalDB test configuration. Original story: `US-129-end-to-end-journey.md` / AC-64.
