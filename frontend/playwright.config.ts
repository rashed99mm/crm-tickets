import { defineConfig, devices } from '@playwright/test';

/**
 * AC-64 / US-129 — the single terminal end-to-end journey for slice S1.
 *
 * Deliberately one config for one spec file: the spec (`2026-08-24-ticket-lifecycle-design.md`)
 * defines exactly one browser journey, and adding more would mean amending an approved spec.
 * Each feature's own gate is served by its unit, integration and component tests instead.
 *
 * This starts the Angular dev server itself (`ng serve admin-app`), but NOT the backend — the
 * InternalApi needs `ConnectionStrings__DefaultConnection` and `Jwt__Key` set per CLAUDE.md and
 * must already be running at http://localhost:5074 (the proxy target in `proxy.conf.json`) before
 * `npx playwright test` is run.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  expect: { timeout: 5_000 },
  fullyParallel: false,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    command: 'npx ng serve admin-app --port 4200',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env['CI'],
    timeout: 120_000,
  },
});
