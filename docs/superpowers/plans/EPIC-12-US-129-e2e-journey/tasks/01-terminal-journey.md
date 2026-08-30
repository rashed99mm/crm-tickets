# Task 01 — Terminal Browser Journey

**Story/AC:** US-129, AC-64 / AC-129.1 / AC-129.2
**Layer:** Frontend E2E with real backend/database
**Status:** not started

## Executable checklist

- [ ] Audit `frontend/e2e/journey.spec.ts`, `playwright.config.ts`, and the six production templates
  listed in the parent plan; classify every locator and preserve one journey only.
- [ ] First write/adjust the failing test in `frontend/e2e/journey.spec.ts` so it creates unique
  customer data, creates a ticket, asserts assignment/status options exist, assigns, changes status,
  calls `page.reload()`, and asserts the persisted status plus both history events.
- [ ] Start InternalApi on `http://localhost:5074` with LocalDB and `Jwt__Key`, then start the Angular
  admin app at the Playwright base URL. Do not use an API mock or test-only bypass.
- [ ] Run the targeted Playwright command and capture the first failure.
- [ ] Fix the real production label/test ID, route, authorization, API, or persistence defect in the
  owning file. Never remove reload or weaken an assertion.
- [ ] Rerun the journey, then run the frontend build and affected tests; paste actual output into the
  story status evidence.
- [ ] Mark AC-64 complete only when the post-reload assertions pass and record any deviation below.

## Exact files

- Primary test: `frontend/e2e/journey.spec.ts`.
- Configuration: `frontend/playwright.config.ts`.
- Possible production fixes: `login.component.html`, `customer-create.component.html`,
  `ticket-create.component.html`, `ticket-detail.component.html`, `ticket-detail.component.ts`,
  `app.routes.ts`, and the relevant common API service.
- Backend fixes only if the failure is real persistence/contract behavior:
  `AuthController.cs`, `CustomersController.cs`, `TicketsController.cs`, or the owning handler/test.

## Verification commands

```powershell
cd frontend
npx playwright test e2e/journey.spec.ts --project=chromium
npx ng build admin-app
npx ng test admin-app --watch=false
```

## Status evidence

Record server configuration without secrets, browser/test command, pass/fail count, trace location,
and the post-reload values asserted. No build or test command has been run while writing this plan.

## Deviation record

`None yet.` Record account substitution, missing seed data, selector changes, or a backend defect as
an explicit fact and link the regression test that covers it.
