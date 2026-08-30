import { expect, test } from '@playwright/test';

/**
 * US-129 / AC-64 — the one terminal journey for slice S1.
 *
 * "Sign in, create a ticket, assign it, change its status, reload, and confirm the change and its
 * history persisted." The reload is the point (see the story's Notes): everything before it can
 * pass against state held in a component: only the reload proves it reached the database and came
 * back.
 *
 * NOT YET RUN. Scaffolded while the backend build is broken mid-refactor (see
 * `docs/superpowers/specs/2026-08-26-refactor-sprint-design.md`), so none of the assumptions below
 * are verified yet:
 *
 * - The seeded admin (`admin@cce-platform.com` / `Admin@123456`, per `CLAUDE.md`) is assumed to
 *   pass the supervisor-only assign gate. If the seed data has a distinct Supervisor account, swap
 *   these credentials for it — Admin was chosen only because it is the one account CLAUDE.md
 *   documents as guaranteed to exist.
 * - A customer is created inline (`/customers/new`) rather than relying on seed data, so the
 *   journey does not depend on what else has been seeded.
 * - The assignee is "whichever agent the dropdown offers first" rather than a named agent, for the
 *   same reason.
 *
 * Run with `npx playwright test` once the backend builds and is reachable at :5074 with
 * `ConnectionStrings__DefaultConnection` and `Jwt__Key` set.
 */
test('sign in, create a ticket, assign it, change status, reload, and the change persists', async ({
  page,
}) => {
  // ── Sign in (FEAT-02, AC-55/56) ──────────────────────────────────────────────────────────
  await page.goto('/login');
  await page.getByLabel('Email').fill('admin@cce-platform.com');
  await page.getByLabel('Password').fill('Admin@123456');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page).toHaveURL(/\/dashboard$/);

  // ── Create a customer to attach the ticket to ───────────────────────────────────────────
  const stamp = Date.now();
  await page.goto('/customers/new');
  await page.getByLabel('Name').fill(`E2E Journey Customer ${stamp}`);
  await page.getByLabel('Email').fill(`e2e-journey-${stamp}@example.test`);
  await page.getByRole('button', { name: 'Create customer' }).click();
  await expect(page).toHaveURL(/\/customers\/[0-9a-f-]{36}$/);

  // ── Create a ticket (FEAT-04, AC-29..31, AC-59/60) ──────────────────────────────────────
  await page.goto('/tickets/new');
  await page.getByLabel('Subject').fill(`E2E journey ticket ${stamp}`);
  await page
    .locator('#ticket-customer')
    .selectOption({ label: new RegExp(`E2E Journey Customer ${stamp}`) });
  // First real option after the "select…" placeholder — the journey does not depend on which
  // category or priority exists, only that one is chosen.
  await page.locator('#ticket-category').selectOption({ index: 1 });
  await page.locator('#ticket-priority').selectOption({ index: 0 });
  await page.getByLabel('Description').fill('Created by the terminal end-to-end journey (US-129).');
  await page.getByRole('button', { name: 'Create ticket' }).click();

  await expect(page).toHaveURL(/\/tickets\/[0-9a-f-]{36}$/);
  const ticketUrl = page.url();

  // ── Assign it (FEAT-07, AC-42/44) ───────────────────────────────────────────────────────
  const assignSelect = page.locator('[data-testid="assign-action"] select');
  await assignSelect.selectOption({ index: 1 });
  const assignedAgentLabel = await assignSelect
    .locator('option:checked')
    .first()
    .textContent();

  // ── Change its status (FEAT-06, AC-37) ──────────────────────────────────────────────────
  const statusSelect = page.locator('[data-testid="status-action"] select');
  await statusSelect.selectOption({ index: 1 });
  const newStatusLabel = await statusSelect.locator('option:checked').first().textContent();

  // ── Reload — the point of the whole test ────────────────────────────────────────────────
  await page.goto(ticketUrl);
  await page.reload();

  // The status the previous step set is still shown after the round trip.
  await expect(page.locator('[data-testid="status-action"]')).toContainText(
    (newStatusLabel ?? '').trim(),
  );

  // And the history timeline records both changes — not just the current state, but that a
  // change happened, which is what AC-64 actually asks the reload to prove.
  const history = page.locator('[data-testid="history-timeline"]');
  await expect(history).toContainText((assignedAgentLabel ?? '').trim());
  await expect(history).toContainText((newStatusLabel ?? '').trim());
});
