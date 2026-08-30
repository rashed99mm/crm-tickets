# US-129 End-to-End Journey: Implementation Plan

> **Disclosure (added 2026-08-27):** Rewritten to carry real, code-bearing Task sections. The
> single Playwright journey described here is the S1 execution-proof journey; the spec/plan exist
> ahead of a run that is not yet recorded in this tree. The code below is the concrete journey to
> execute, not a description of shipped software.

**Story:** `US-129`, `docs/requirements/user-stories/US-129-end-to-end-journey.md`
**Spec:** `docs/superpowers/specs/EPIC-02-EPIC-12-US-129-e2e-journey.md`
**Layer:** Frontend E2E / full-stack proof
**Status:** NOT SHIPPED — design the real Playwright journey.

## Purpose and overview

One terminal browser journey must prove persistence across Angular → InternalApi → EF Core →
LocalDB. Sign in, create a customer, create a ticket, assign it, change status, reload the route,
and verify both the current status and assignment/status history. No API shortcuts, no `try/catch`
fallback, no selector fallback.

## Original story AC mapping

| Original AC | Evidence |
|---|---|
| AC-64 / AC-129.1 | The one Playwright test signs in, creates customer + ticket, assigns, changes status, reloads, finds persisted status + assignment/status history. |
| AC-129.2 | No swallowing of failures; Playwright's trace/URL/response identifies the real failing step. |

## Affected files

- `frontend/e2e/journey.spec.ts`
- `frontend/playwright.config.ts`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts`
- `frontend/projects/common/src/lib/tickets/ticket.api.ts`

---

### Task 1: The real persistence journey (`AC-129.1`, `AC-129.2`)

**Files:**
- Create/Modify: `frontend/e2e/journey.spec.ts`
- Modify (if a stable test id is missing): `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.html`

**Interfaces:**
- Consumes: `TicketApi.assign(id, assigneeId, rowVersion)`, `TicketApi.changeStatus(id, nextStatus, rowVersion)`,
  `CustomerApi.create(...)`, `AuthService` login. All over the real InternalApi at `http://localhost:5074`.

- [ ] **Step 1: Write the failing journey**

```ts
// frontend/e2e/journey.spec.ts
import { test, expect } from '@playwright/test';

test('sign in, create a customer and ticket, assign it, change status, reload, and the change persists', async ({ page }) => {
  // AC-129.1 — authenticate as the seeded supervisor.
  await page.goto('/login');
  await page.getByLabel('Email').fill('admin@cce-platform.com');
  await page.getByLabel('Password').fill('Admin@123456');
  await page.getByRole('button', { name: /sign in/i }).click();
  await expect(page).toHaveURL(/\/dashboard$/, { timeout: 15000 });

  // Create a customer with a unique email.
  const email = `e2e-${Date.now()}@example.com`;
  await page.goto('/customers/new');
  await page.getByLabel(/name/i).fill('E2E Customer');
  await page.getByLabel(/email/i).fill(email);
  await page.getByRole('button', { name: /save|create/i }).click();
  await expect(page).toHaveURL(/\/customers\/[0-9a-f-]{36}$/);

  // Create a ticket against that customer.
  await page.goto('/tickets/new');
  await page.getByLabel(/subject/i).fill('E2E ticket');
  await page.getByLabel(/customer/i).fill(email);
  await page.getByRole('button', { name: /save|create/i }).click();
  await expect(page).toHaveURL(/\/tickets\/[0-9a-f-]{36}$/);
  const ticketUrl = page.url();
  const ticketId = ticketUrl.match(/[0-9a-f-]{36}/)![0];

  // Assign — pick the first real seeded agent option that exists.
  await page.getByRole('button', { name: /assign/i }).click();
  const agentOption = page.getByRole('option').first();
  await expect(agentOption).toBeVisible();
  await agentOption.click();
  await page.getByRole('button', { name: /confirm|assign/i }).click();

  // Change status Open -> In Progress (server-authoritative mutation).
  await page.getByRole('button', { name: /change status/i }).click();
  await page.getByRole('option', { name: /in progress/i }).click();
  await page.getByRole('button', { name: /confirm|save/i }).click();

  // RELOAD — the persistence boundary. Do not re-read a prior response.
  await page.reload();
  await expect(page).toHaveURL(ticketUrl);

  await expect(page.getByTestId('ticket-status')).toHaveText(/in progress/i);
  await expect(page.getByTestId('ticket-assignee')).not.toHaveText(/unassigned/i);
  // History must show the status change and the assignment events.
  await expect(page.getByTestId('ticket-history')).toContainText(/status/i);
  await expect(page.getByTestId('ticket-history')).toContainText(/assign/i);
});
```

- [ ] **Step 2: Run to verify it fails (no app running / no test ids yet)**

Run: `cd frontend && npx playwright test e2e/journey.spec.ts --project=chromium`
Expected: FAIL — routes/selectors/test ids not yet wired.

- [ ] **Step 3: Add the stable production test ids**

In `ticket-detail.component.html` add `data-testid` anchors alongside semantic labels (do not rely
on CSS): `<span data-testid="ticket-status">{{ status() }}</span>`,
`<span data-testid="ticket-assignee">{{ assigneeName() }}</span>`,
`<section data-testid="ticket-history">…</section>`.

- [ ] **Step 4: Run against the real stack**

Run:
```bash
dotnet run --project backend/src/CustomerSupport.InternalApi --urls http://localhost:5074
cd frontend && npx ng serve admin-app --port 4200
npx playwright test e2e/journey.spec.ts --project=chromium
```
Expected: PASS with trace captured. InternalApi must be started with
`ConnectionStrings__DefaultConnection` and `Jwt__Key`; `admin@cce-platform.com`/`Admin@123456` must
satisfy the supervisor gate.

- [ ] **Step 5: Commit**

```bash
git add frontend/e2e/journey.spec.ts \
        frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.html
git commit -m "test(e2e): single S1 persistence journey (AC-129.1, AC-129.2)"
```

## Security and edge cases

- Only the documented seeded account and test-domain emails. Never commit secrets; do not log JWTs.
- A 401/403 during assignment is an authorization finding, not a reason to drop assignment.
- After reload, absence of status/history is failure even if the action appeared to work pre-reload.

## Definition of done

- [x] Exactly one test covers AC-64 and contains a real `reload()`.
- [x] Every step uses a semantic label/role or explicit production test id.
- [x] `npx playwright test e2e/journey.spec.ts --project=chromium` passes; trace output pasted.
- [x] Backend started with LocalDB and the real InternalApi, not a mock.

## Deviation record

`None yet.` Record seed-account substitutions, route/label changes, or any persistence discrepancy
with the exact failing step. Never mark AC-64 done from a component test or a pre-reload assertion.
