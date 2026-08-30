# Task 19 — End-to-end journey (US-129)

## Traceability
Epic:   docs/requirements/epics/EPIC-02-ticket-management.md (terminal journey)
        + docs/requirements/epics/EPIC-13-mockup-fidelity.md
Story:  docs/requirements/user-stories/US-129-end-to-end-journey.md
FEAT:   FEAT-11 (end-to-end journey) — delivery-plan.md, sprint 4
Spec:   docs/superpowers/specs/EPIC-02-EPIC-12-US-129-e2e-journey.md
Plan:   docs/superpowers/plans/EPIC-12-US-129-e2e-journey/

## Work
frontend/e2e/journey.spec.ts exists (untracked) — make it pass against both hosts, extended to
the full feature arc: signup → submit ticket → agent assigns → status change → reply →
survey (CSAT) → KB vote → AI suggestion visible → CSAT row on reports. This is the ONLY E2E
(AC-64) — do not add more.

## Gate
npx playwright test e2e/journey.spec.ts → passing, output pasted (both hosts running).
