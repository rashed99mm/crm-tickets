# Task 04: Portal, Reports, Admin, And Verification

**Status:** Partially implemented  
**Criteria:** `AC-512`, `AC-513`, `AC-514`, `AC-515`, `AC-516`, `AC-517`, `AC-518`, `AC-519`, `AC-520`  
**Scope:** Customer portal, knowledge base, reports/charts, administration, integrations, final
quality gates.

## Files To Read First

- `frontend/projects/portal-app/src/app/features/home/*`
- `frontend/projects/portal-app/src/app/features/dashboard/*`
- `frontend/projects/portal-app/src/app/features/tickets/*`
- `frontend/projects/portal-app/src/app/features/kb/*`
- `frontend/projects/portal-app/src/app/features/live-chat/*`
- `frontend/projects/portal-app/src/app/features/web-form/*`
- `frontend/projects/admin-app/src/app/features/kb/*`
- `frontend/projects/admin-app/src/app/features/reports/*`
- `frontend/projects/admin-app/src/app/features/admin/*`
- `frontend/projects/admin-app/src/app/features/users/*`
- `frontend/projects/admin-app/src/app/features/organisation/*`
- `frontend/projects/common/src/lib/reports/**`
- `frontend/e2e/*.spec.ts`

## Intent

Complete the refactor across customer-facing and management surfaces, then verify the UI against the
SDD acceptance criteria: portal usability, KB clarity, readable charts, admin safety, accessibility,
builds, and responsive visual review.

Use `management_analytics_sla_performance` as the reference for report density, chart framing, KPI
priority, legends, and table fallbacks. Use the portal portions of the existing CRM mockups for a
mobile-first customer flow that stays visually related to the staff app without exposing staff-only
workflow controls.

## Required Changes

- Portal: improve submit-ticket, my tickets, ticket detail/reply, FAQ/KB, live chat/web form, and
  feedback flows for mobile-first customer use.
- Knowledge base: make staff authoring and customer browsing/search visually distinct but consistent.
- Reports: add readable chart compositions for ticket volume, SLA performance, agent performance,
  live queue, and CSAT where data exists. Pair every chart with labels and a table/fallback.
- Admin: improve users, permissions, audit logs, platform settings, departments, branches, SLA
  policies, and integration/provider settings with clear forms, dialogs, save/cancel states, and
  danger affordances.
- Verification: update or add Playwright coverage for critical routes at 375px, 768px, 1280px, and
  1920px.
- Error/reload: chart panels, portal ticket flows, KB pages, and admin forms must render retryable
  errors and clear empty states inside the affected panel instead of leaving blank regions.
- AI usage: portal/customer-facing AI chatbot entry points must be separated from staff-only AI
  workflow controls; staff reports may show AI-powered insight placeholders only when backed by an
  existing API or honest unavailable state.

## Implementation Notes

- Do not add a chart dependency without checking `frontend/package.json` first. If a dependency is
  needed, document why in the deviation register.
- Blank canvases are failures. Use empty/error states in chart regions.
- Export/report buttons must be visible, disabled when not supported, and permission-aware.
- Portal copy must not reveal staff-only concepts or admin-only actions.

## Code Context And Examples

`frontend/package.json` currently has no charting dependency. Prefer CSS/SVG-light charts for the
first pass unless the report UI genuinely needs a library.

Example no-dependency bar chart:

```html
<cs-chart-frame
  [title]="'reports.ticketVolume.title' | t"
  [loading]="loading()"
  [empty]="rows().length === 0"
  [error]="error()?.message ?? null"
>
  <div class="flex h-56 items-end gap-3" role="img" [attr.aria-label]="'reports.ticketVolume.chartLabel' | t">
    @for (row of rows(); track row.label) {
      <div class="flex min-w-10 flex-1 flex-col items-center gap-2">
        <div
          class="w-full rounded-t bg-primary"
          [style.height.%]="maxCount() === 0 ? 0 : (row.count / maxCount()) * 100"
          [title]="row.label + ': ' + row.count"
        ></div>
        <span class="max-w-full truncate text-label-md text-on-surface-variant">{{ row.label }}</span>
      </div>
    }
  </div>

  <table class="mt-4 w-full text-start text-body-sm">
    <!-- tabular fallback -->
  </table>
</cs-chart-frame>
```

Example portal ticket card:

```html
<article class="rounded-lg border border-border-subtle bg-surface-lowest p-4">
  <div class="flex items-start justify-between gap-3">
    <div class="min-w-0">
      <p class="font-mono text-data-mono text-on-surface-variant">{{ ticket.reference }}</p>
      <h2 class="truncate font-display text-headline-md text-on-surface">{{ ticket.subject }}</h2>
    </div>
    <cs-badge kind="status" [value]="ticket.status" />
  </div>
  <dl class="mt-3 grid grid-cols-2 gap-3 text-body-sm">
    <div>
      <dt class="text-on-surface-variant">{{ 'tickets.category' | t }}</dt>
      <dd class="text-on-surface">{{ ticket.categoryName }}</dd>
    </div>
    <div>
      <dt class="text-on-surface-variant">{{ 'tickets.updatedAt' | t }}</dt>
      <dd class="text-on-surface">{{ ticket.updatedAt | csDate }}</dd>
    </div>
  </dl>
</article>
```

Example admin danger affordance:

```html
<cs-dialog [open]="confirmDeleteOpen()" [heading]="'admin.confirmDelete.title' | t">
  <p class="text-body-md text-on-surface-variant">
    {{ 'admin.confirmDelete.message' | t }}
  </p>
  <cs-action-bar>
    <cs-button variant="secondary" (pressed)="confirmDeleteOpen.set(false)">
      {{ 'common.cancel' | t }}
    </cs-button>
    <cs-button variant="danger" [busy]="deleting()" (pressed)="deleteSelected()">
      <cs-icon name="delete" />
      {{ 'common.delete' | t }}
    </cs-button>
  </cs-action-bar>
</cs-dialog>
```

Example Playwright matrix:

```ts
const viewports = [
  { width: 375, height: 812 },
  { width: 768, height: 1024 },
  { width: 1280, height: 900 },
  { width: 1920, height: 1080 },
];

for (const viewport of viewports) {
  test(`AC519 dashboard has no horizontal overflow at ${viewport.width}`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await page.goto('/dashboard');
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(overflow).toBe(false);
  });
}
```

## Suggested Tests

- `AC512_PortalTicketFlowIsMobileFriendlyAndCustomerScoped`
- `AC513_KnowledgeBaseSearchAndEmptyStatesAreClear`
- `AC514_ReportChartsAreLabeledFilterableAndHaveTableFallbacks`
- `AC515_ReportFailuresDoNotRenderBlankCharts`
- `AC516_AdminRiskyActionsHaveClearAffordances`
- `AC519_ResponsiveVisualMatrixHasNoCriticalLayoutDefects`

## Verification

Run from `frontend/`:

```text
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
npx playwright test
```

## Execution Record

| Item | Result |
|---|---|
| Tests added | No new portal/report tests; existing portal tests were kept green after RTL/localization cleanup and ticket detail compatibility aliases. |
| Commands run | `npx ng test portal-app --watch=false` passed: 14 files, 55 tests. `npx ng build portal-app` passed with the existing initial bundle budget warning: 612.79 kB versus 560 kB. |
| Deviations | Fixed hardcoded/RTL template offenders in KB, CSAT, live queue, portal home, public shell, and portal ticket detail. Report pages do not yet consume `cs-chart-frame`; Playwright visual matrix was not run in this pass. |
| Commit | Pending |
