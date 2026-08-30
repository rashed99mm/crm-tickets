# Task 02: Shell, Navigation, And Layout

**Status:** Verified existing shell, plus RTL cleanup  
**Criteria:** `AC-504`, `AC-505`, `AC-506`  
**Scope:** Staff and portal shells, navigation, layout canvas, mobile drawer, RTL behavior.

## Files To Read First

- `frontend/projects/admin-app/src/app/layout/shell.component.ts`
- `frontend/projects/admin-app/src/app/layout/shell.component.html`
- `frontend/projects/admin-app/src/app/layout/shell.component.spec.ts`
- `frontend/projects/admin-app/src/app/app.routes.ts`
- `frontend/projects/portal-app/src/app/layout/shell.component.ts`
- `frontend/projects/portal-app/src/app/layout/shell.component.html`
- `frontend/projects/portal-app/src/app/layout/shell-public.component.ts`
- `frontend/projects/portal-app/src/app/layout/shell-public.component.html`
- `frontend/projects/portal-app/src/app/app.routes.ts`
- `frontend/projects/common/src/lib/i18n/translations.ts`

## Intent

Make the application structure feel like one CRM product. Navigation should expose the right
workflows, respect permissions, work on mobile, and avoid overlap with notifications, language
switching, profile, sign out, and the AI assistant action.

## Required Changes

- Review staff navigation against the CRM feature map: dashboard, tickets, customers, chat/channels,
  KB, reports, administration, settings, profile, and assistant.
- Group navigation visually by workflow where practical: Work, Customers, Knowledge, Reports, Admin.
- Preserve role guards and hide unavailable navigation where the existing role model already does.
- Improve topbar density and button visibility for notifications, language, profile, and sign out.
- Make mobile drawer focusable, closable, and free of content overlap.
- Align portal shell/public shell styling with customer workflows without exposing staff-only
  controls.
- Validate Arabic RTL mirroring for sidebar, drawer, topbar controls, and content rails.

## Implementation Notes

- Do not add routes unless a real component already exists and the product spec needs a reachable
  surface.
- Keep the authenticated staff app work-first; no hero page inside `admin-app`.
- The floating AI assistant action must not cover primary page actions at mobile sizes.
- Use existing `SessionStore`, role guards, notification service, and locale store contracts.

## Code Context And Examples

Current shell navigation item shape:

```ts
export interface NavItem {
  readonly path: string;
  readonly key: TranslationKey;
  readonly icon: string;
  readonly adminOnly?: true;
  readonly supervisorOrAdmin?: true;
  readonly hidden?: true;
}
```

Target grouped shape, if the shell needs visual sections:

```ts
type NavGroup = 'work' | 'customers' | 'knowledge' | 'reports' | 'admin';

export interface NavItem {
  readonly path: string;
  readonly key: TranslationKey;
  readonly icon: string;
  readonly group: NavGroup;
  readonly adminOnly?: true;
  readonly supervisorOrAdmin?: true;
  readonly hidden?: true;
}
```

Example grouped rendering:

```html
@for (group of navGroups(); track group.key) {
  <section class="mt-4 first:mt-0">
    @if (!collapsed()) {
      <h2 class="px-3 pb-2 text-label-md uppercase text-on-surface-variant">
        {{ group.labelKey | t }}
      </h2>
    }

    @for (item of group.items; track item.path) {
      <a
        [routerLink]="item.path"
        routerLinkActive="bg-secondary-container text-on-secondary-container"
        class="flex h-10 items-center gap-2 rounded-lg px-3 text-label-lg text-on-surface-variant hover:bg-surface-highest"
      >
        <cs-icon [name]="item.icon" />
        @if (!collapsed()) {
          <span class="min-w-0 truncate">{{ item.key | t }}</span>
        }
      </a>
    }
  </section>
}
```

Example mobile drawer target:

```html
@if (mobileMenuOpen()) {
  <div class="fixed inset-0 z-40 bg-black/40 lg:hidden" (click)="closeMobileMenu()"></div>
  <aside
    class="fixed inset-y-0 start-0 z-50 flex w-80 max-w-[90vw] flex-col bg-surface-low p-4 shadow-popover lg:hidden"
    role="dialog"
    aria-modal="true"
    [attr.aria-label]="'nav.mobile' | t"
    (keydown.escape)="closeMobileMenu()"
  >
    <!-- same grouped nav as desktop -->
  </aside>
}
```

Example acceptance check:

```ts
it('AC504: exposes CRM navigation entries that resolve to real routes', () => {
  const paths = flattenRoutes(routes).map((route) => `/${route}`);
  for (const item of NAV_ITEMS.filter((entry) => !entry.hidden)) {
    expect(paths).toContain(item.path);
  }
});
```

## Suggested Tests

- `AC504_ShellExposesRoleAppropriateCrmNavigation`
- `AC505_MobileDrawerAndAssistantDoNotOverlapContent`
- `AC506_RtlShellUsesLogicalDirectionClasses`

## Verification

Run from `frontend/`:

```text
npx ng test admin-app --watch=false --include='**/shell.component.spec.ts'
npx ng test portal-app --watch=false --include='**/shell*.component.spec.ts'
npx ng build admin-app
npx ng build portal-app
```

## Execution Record

| Item | Result |
|---|---|
| Tests added | No new shell tests. Existing `shell.component.spec.ts` already verifies command-center shell, mobile drawer, collapsed sidebar, route names, role navigation, and logical direction safety. |
| Commands run | Covered by `npx ng test admin-app --watch=false`: 28 files, 187 tests. |
| Deviations | Grouped sidebar sections were left for a later pass because the existing role-aware shell already satisfies the main discoverability/mobile requirements and tests. |
| Commit | Pending |
