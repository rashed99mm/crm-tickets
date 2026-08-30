# Frontend CRM UX Refactor Implementation Plan

**Spec:** [`../../specs/EPIC-01-US-101-frontend-crm-ux-refactor-design.md`](../../specs/EPIC-01-US-101-frontend-crm-ux-refactor-design.md)  
**Status:** Active  
**Layer:** Frontend only  
**Architecture:** Angular 21 standalone apps with shared `common` UI library and Tailwind v4 tokens.

## Objective

Make the frontend match the Customer Support CRM product specification as a usable operational
interface. The refactor is visual, interaction, information architecture, responsive, and
accessibility work over existing features; it does not add backend behavior.

## Design Reference

Use `stitch_smart_support_ticketing_crm` as the UX source of truth for page composition, then adapt
it to the existing Angular codebase and data contracts:

| Reference | Implementation target |
|---|---|
| `ticket_detail_chatbot` | Ticket detail header band, metadata strip, timeline, conversation area, AI assist side rail |
| `ai_powered_agent_workspace` | Agent dashboard, assigned ticket queue, quick actions, AI summary/reply/category/solution flow |
| `customer_360_history` | Customer profile context, interaction history, notes, attachments, upload/download/remove states |
| `management_analytics_sla_performance` | Report KPI panels, chart frames, legends, empty/error chart states, table fallback |

## Execution Sequence

| Task | File | Commit boundary |
|---|---|---|
| 01 | [`tasks/task-01-shared-ux-foundation.md`](tasks/task-01-shared-ux-foundation.md) | shared design primitives |
| 02 | [`tasks/task-02-shell-navigation-layout.md`](tasks/task-02-shell-navigation-layout.md) | app shells and navigation |
| 03 | [`tasks/task-03-core-staff-workflows.md`](tasks/task-03-core-staff-workflows.md) | dashboard, tickets, customers |
| 04 | [`tasks/task-04-portal-reports-admin-verification.md`](tasks/task-04-portal-reports-admin-verification.md) | portal, KB, reports, admin, final checks |

## Frontend File Map

| Area | Primary files |
|---|---|
| Shared tokens | `frontend/projects/common/src/styles/theme.css` |
| Shared UI | `frontend/projects/common/src/lib/ui/*.component.{ts,html,spec.ts}` |
| Shared i18n | `frontend/projects/common/src/lib/i18n/translations.ts` |
| Staff shell | `frontend/projects/admin-app/src/app/layout/shell.component.{ts,html,spec.ts}` |
| Staff routes | `frontend/projects/admin-app/src/app/app.routes.ts` |
| Agent dashboard | `frontend/projects/admin-app/src/app/features/dashboard/dashboard.component.{ts,html,spec.ts}` |
| Tickets | `frontend/projects/admin-app/src/app/features/tickets/*.{ts,html,spec.ts}` |
| Customers | `frontend/projects/admin-app/src/app/features/customers/*.{ts,html,spec.ts}` |
| Chat/channels | `frontend/projects/admin-app/src/app/features/chat/*.{ts,html,spec.ts}`, `frontend/projects/common/src/lib/channels/**` |
| Knowledge base | `frontend/projects/admin-app/src/app/features/kb/*`, `frontend/projects/portal-app/src/app/features/kb/*`, `frontend/projects/common/src/lib/contents/**` |
| Reports | `frontend/projects/admin-app/src/app/features/reports/*.{ts,html,spec.ts}`, `frontend/projects/common/src/lib/reports/**` |
| Admin | `frontend/projects/admin-app/src/app/features/admin/*`, `features/users/*`, `features/organisation/*` |
| Portal shell | `frontend/projects/portal-app/src/app/layout/*.{ts,html,spec.ts}` |
| Portal workflows | `frontend/projects/portal-app/src/app/features/**` |
| E2E | `frontend/e2e/*.spec.ts`, `frontend/playwright.config.ts` |

## Required Implementation Pattern

1. Read the task file and the linked spec criteria.
2. Inspect the live component before editing; do not assume names from the plan are current.
3. Add or update focused tests first where practical.
4. Implement using shared primitives and translation keys.
5. Run focused tests, then the relevant app build.
6. Update the task execution record with command output summary and any deviations.

## Error And Reload Rules

Apply these rules in every task:

```ts
// Route or panel data should collapse into explicit async state before rendering.
type SurfaceState<T> =
  | { status: 'loading' }
  | { status: 'loaded'; data: T }
  | { status: 'empty' }
  | { status: 'error'; error: ApiError };
```

```html
@switch (state().status) {
  @case ('loading') {
    <cs-loading-state [label]="loadingLabel" />
  }
  @case ('error') {
    <cs-error-state [error]="state().error" (retry)="load()" />
  }
  @case ('empty') {
    <cs-empty-state [message]="emptyMessage" />
  }
  @default {
    <ng-content />
  }
}
```

- Retry must call the same `load()` or panel refresh method used on initial render.
- Mutation failures must keep the refusal visible while reloading the current record.
- Chart frames must render loading, empty, unavailable, or error content inside the chart area.
- Upload/download/remove errors stay local to the attachments panel.

## AI Panel Rules

```html
<aside class="flex flex-col gap-3 border border-border-subtle bg-surface-lowest p-4">
  <header class="flex items-center gap-2">
    <cs-icon name="auto_awesome" />
    <h2>{{ 'ai.assist.title' | t }}</h2>
  </header>

  <cs-button variant="secondary" (pressed)="summarise()">
    <cs-icon name="summarize" [size]="16" />
    {{ 'ai.summary' | t }}
  </cs-button>
</aside>
```

- Keep AI beside the work surface, not above it.
- AI suggestions must be reviewable with accept/reject controls before they change workflow state.
- Suggested solutions must cite KB articles through existing routes.
- Missing AI capability must show unavailable state, not hidden blank space.

## Existing Code Context

The plan starts from these live contracts:

```ts
// frontend/projects/common/src/lib/ui/button.component.ts
export class CsButton {
  readonly variant = input<'primary' | 'secondary' | 'ghost'>('primary');
  readonly type = input<'button' | 'submit'>('button');
  readonly disabled = input(false);
  readonly busy = input(false);
  readonly pressed = output<void>();
}
```

```ts
// frontend/projects/common/src/lib/ui/badge.component.ts
export class CsBadge {
  readonly kind = input.required<'status' | 'priority'>();
  readonly value = input.required<string>();
  readonly label = input<string>();
}
```

```ts
// frontend/projects/admin-app/src/app/layout/shell.component.ts
export interface NavItem {
  readonly path: string;
  readonly key: TranslationKey;
  readonly icon: string;
  readonly adminOnly?: true;
  readonly supervisorOrAdmin?: true;
  readonly hidden?: true;
}
```

The refactor should extend these contracts carefully instead of replacing them. Where a new visual
state is needed, prefer additive inputs and literal class maps so existing call sites keep compiling.

## Suggested Shared Additions

These are plan-level examples, not mandatory exact code. Implementers must adapt them to the live
files after reading the current source.

```ts
// common/src/lib/ui/action-bar.component.ts
@Component({
  selector: 'cs-action-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class:
      'sticky bottom-0 z-10 flex min-h-14 flex-wrap items-center justify-end gap-2 border-t border-border-subtle bg-surface-lowest/95 px-4 py-3 backdrop-blur',
  },
  template: '<ng-content />',
})
export class CsActionBar {}
```

```ts
// common/src/lib/ui/chart-frame.component.ts
@Component({
  selector: 'cs-chart-frame',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './chart-frame.component.html',
})
export class CsChartFrame {
  readonly title = input.required<string>();
  readonly loading = input(false);
  readonly empty = input(false);
  readonly error = input<string | null>(null);
}
```

```html
<!-- common/src/lib/ui/chart-frame.component.html -->
<section class="min-h-72 rounded-lg border border-border-subtle bg-surface-lowest p-4">
  <h2 class="font-display text-headline-md text-on-surface">{{ title() }}</h2>
  @if (loading()) {
    <cs-loading-state />
  } @else if (error()) {
    <cs-error-state [message]="error()!" />
  } @else if (empty()) {
    <cs-empty-state />
  } @else {
    <div class="mt-4 min-h-52">
      <ng-content />
    </div>
  }
</section>
```

```ts
// common/src/lib/ui/channel-pill.component.ts
const CHANNEL_TONE: Readonly<Record<string, string>> = {
  email: 'border-sky-200 bg-sky-50 text-sky-700',
  whatsapp: 'border-emerald-200 bg-emerald-50 text-emerald-700',
  chat: 'border-violet-200 bg-violet-50 text-violet-700',
  sms: 'border-amber-200 bg-amber-50 text-amber-700',
  web: 'border-slate-200 bg-slate-50 text-slate-700',
};
```

## Visual Rules

- Use icons for icon-friendly commands such as menu, close, search, filter, edit, delete, assign,
  reply, export, refresh, notification, AI, language, and collapse.
- Use compact buttons and action bars for operational screens.
- Use charts only where data shape supports it; every chart needs labels and a tabular fallback.
- Keep cards for repeated items, dialogs, and genuinely framed tools. Do not wrap whole page
  sections in nested cards.
- Do not create a one-color UI. The CRM must distinguish brand, neutral surfaces, status, priority,
  warning, success, danger, channel, and AI states.

## Verification Matrix

| Check | Required command or action |
|---|---|
| Shared UI tests | `cd frontend && npx ng test common --watch=false` |
| Staff app tests | `cd frontend && npx ng test admin-app --watch=false` |
| Portal app tests | `cd frontend && npx ng test portal-app --watch=false` |
| Staff build | `cd frontend && npx ng build admin-app` |
| Portal build | `cd frontend && npx ng build portal-app` |
| Visual/responsive | `cd frontend && npx playwright test` |
| Manual review | Desktop and mobile pass over dashboard, tickets, customers, reports, admin, portal |

## Deviation Register

Add entries here during execution.

| Date | Task | Deviation | Reason | Follow-up |
|---|---|---|---|---|
| 2026-08-28 | Initial plan | None | Plan created before implementation | N/A |
| 2026-08-28 | 01 | `cs-chart-frame` added but not adopted in report screens | Kept first code pass scoped to shared foundation and queue visibility | Adopt in report pages and add chart/fallback tests |
| 2026-08-28 | 02 | Sidebar grouping not implemented | Existing shell already has role-aware navigation, collapsed desktop rail, mobile drawer, language, notifications, profile, and AI action | Revisit grouped nav after visual review |
| 2026-08-28 | 03 | Ticket detail and customer detail full layout refactor not completed | First pass focused on queue visibility and lifecycle correctness | Continue with ticket/customer detail layouts |
| 2026-08-28 | 04 | Playwright visual matrix not run | Unit/build gates were prioritized for this implementation slice | Run visual matrix after chart/report adoption |
| 2026-08-28 | 01/03 | Shared pagination and CRUD action bars implemented before report chart adoption | User prioritized CRUD buttons, validation flow, and paginated list design | Apply `cs-pagination` to report/admin lists and adopt `cs-chart-frame` next |
| 2026-08-28 | 01 | Inline shared component templates split into `.html` files | Project rule: keep TypeScript and HTML separate | Continue enforcing with template scans before closeout |
| 2026-08-29 | gap audit | Static chat session, branding miswire, KB version/category gaps, audit/user export, AI KB route, and login reset link moved to executable gap-closure plan | Gap report found functional dead controls after the refactor | Continue backend-backed gaps under their owning stories |
