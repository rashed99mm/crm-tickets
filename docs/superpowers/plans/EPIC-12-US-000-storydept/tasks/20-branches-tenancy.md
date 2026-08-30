# Task 20 — Branches / tenancy (LAST, per product decision)

## Traceability
Epic:   docs/requirements/epics/EPIC-12-platform.md (multi-branch)
        + EPIC-01-customer-management.md / EPIC-02-ticket-management.md (scoping)
Stories: US-302-branch-entity.md, EPIC-13-US-306-branch-scoped-queries.md, EPIC-13-US-310-branch-admin-ui.md
FEAT:   FEAT-16 — delivery-plan.md row 7 (US-306 blocked on OQ-5, US-310 not started)
Plans:  plans/EPIC-13-US-306-branch-scoped-queries/, plans/EPIC-13-US-310-branch-admin-ui/

## Work
1. Seed two demo branches + link departments (unblocks the OQ-5 blocker the plan records).
2. Branch admin UI mirroring organisation/departments.component.ts (CRUD, Admin-only route/nav).
3. Branch scoping (US-306): BranchId on tickets/customers queries per the written plan.
4. Department/branch pickers on ticket create.

## Gate
US-306/US-310 named tests green; existing suites unaffected; delivery-plan row 7 updated.
