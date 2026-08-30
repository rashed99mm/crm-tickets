# US-311 Responsive Layout — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing Angular shells and primary workflows usable at 375px, 768px and desktop
widths without JavaScript viewport branching or page-level horizontal overflow.

**Architecture:** CSS-only responsive rules (Tailwind breakpoints `md`=768px, `lg`=1024px) plus a
signal-driven mobile menu in each shell. No `window.innerWidth` reads, no resize listeners, no
`*ngIf` viewport branching. Reuses the existing `collapsed` signal for the desktop icon-rail and adds
a separate `mobileNavOpen` signal for the mobile overlay — the two states are independent and must not
share one boolean.

**Tech Stack:** Angular 20 standalone components, signals, Tailwind v4, existing `CsIcon`/`CsButton`.

**Spec:** `docs/superpowers/specs/EPIC-13-EPIC-13-US-311-responsive-layout.md`

**Not implemented this pass.** This plan is written ahead of any code that implements it, per explicit
instruction — execution is a future session's work.

---

## Global Constraints

- The shell markup already uses logical properties (`border-e`, `ps-`/`pe-`, `start-`/`end-`) — keep
  it that way; responsive changes add `md:`/`lg:`/`max-md:` prefixes, they never introduce `left`/`right`.
- Tables already live inside `overflow-x-auto` regions in `departments.component.html` and the planned
  `branches.component.html`; that pattern is the contract for every list screen. The *document* must
  never scroll horizontally — `US-311` adds a guard test to prove it.
- The mobile overlay menu must keep keyboard semantics honest: `aria-expanded`, `aria-controls`, Escape
  to close, and focus return to the trigger. This is an accessibility requirement, not a nice-to-have.

---

### Task 1: Responsive shells (`AC-311.1`, `AC-311.2`)

**Files:**
- Modify: `frontend/projects/admin-app/src/app/layout/shell.component.ts`
- Modify: `frontend/projects/admin-app/src/app/layout/shell.component.html`
- Modify: `frontend/projects/portal-app/src/app/layout/shell.component.ts`
- Modify: `frontend/projects/portal-app/src/app/layout/shell.component.html`
- Create: `frontend/projects/admin-app/src/app/layout/shell.component.spec.ts`
- Create: `frontend/projects/portal-app/src/app/layout/shell.component.spec.ts`
- Modify: `frontend/projects/common/src/public-api.ts` (no change — `viewChild` is core)

**Interfaces:**
- Consumes: `CsIcon`, `TranslatePipe`, `SessionStore`, `LocaleStore` (already imported in admin shell).
- Produces: `mobileNavOpen` signal, `toggleMobileNav()`, `closeMobileNav()`, `menuTrigger` view-child
  ref for focus return.

- [ ] **Step 1: Write the failing component tests**

```ts
// frontend/projects/admin-app/src/app/layout/shell.component.spec.ts
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { envelopeInterceptor } from 'common';
import { AdminShell } from './shell.component';

describe('AdminShell responsive', () => {
  let fixture: ComponentFixture<AdminShell>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    fixture = TestBed.createComponent(AdminShell);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.verify();
  });

  afterEach(() => http.verify());

  it('AC311_2: exposes a hamburger that toggles an overlay with truthful aria-expanded', () => {
    const trigger = fixture.nativeElement.querySelector('[data-mobile-nav-trigger]') as HTMLButtonElement;
    expect(trigger).toBeTruthy();
    expect(trigger.getAttribute('aria-expanded')).toBe('false');

    trigger.click();
    fixture.detectChanges();

    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    expect(fixture.nativeElement.querySelector('[data-mobile-nav-overlay]')).toBeTruthy();
  });

  it('AC311_2: Escape closes the overlay and returns focus to the trigger', () => {
    const trigger = fixture.nativeElement.querySelector('[data-mobile-nav-trigger]') as HTMLButtonElement;
    trigger.click();
    fixture.detectChanges();

    fixture.nativeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expect(fixture.componentInstance.mobileNavOpen()).toBe(false);
    expect(document.activeElement).toBe(trigger);
  });
});
```

```ts
// frontend/projects/portal-app/src/app/layout/shell.component.spec.ts  (same two specs, PortalShell)
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/layout/shell.component.spec.ts'`
Expected: FAIL — `data-mobile-nav-trigger` / `mobileNavOpen` don't exist yet.

- [ ] **Step 3: Add the signal + focus-return logic to the admin shell**

```ts
// in frontend/projects/admin-app/src/app/layout/shell.component.ts, inside AdminShell
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, viewChild } from '@angular/core';
import { ElementRef } from '@angular/core';

// ... existing imports unchanged ...

  /** Mobile overlay menu state — independent of the desktop icon-rail `collapsed` signal. */
  protected readonly mobileNavOpen = signal(false);

  /** The hamburger trigger, so Escape can return focus to it (AC-311.2). */
  private readonly menuTrigger = viewChild<ElementRef<HTMLButtonElement>>('menuTrigger');

  toggleMobileNav(): void {
    this.mobileNavOpen.update((open) => !open);
  }

  closeMobileNav(): void {
    if (this.mobileNavOpen()) {
      this.mobileNavOpen.set(false);
      // Return focus to the trigger after the DOM settles.
      queueMicrotask(() => this.menuTrigger()?.nativeElement.focus());
    }
  }
```

- [ ] **Step 4: Mark up the admin shell template**

```html
<!-- add inside <div class="flex h-screen overflow-hidden bg-surface">, before <nav> -->
<button
  type="button"
  #menuTrigger
  data-mobile-nav-trigger
  (click)="toggleMobileNav()"
  [attr.aria-expanded]="mobileNavOpen()"
  aria-controls="mobile-nav"
  [attr.aria-label]="'sidebar.menu' | t"
  class="absolute end-3 top-3 z-20 grid size-10 place-items-center rounded-lg text-on-surface-variant hover:bg-surface-high lg:hidden"
>
  <cs-icon name="menu" [size]="24" />
</button>

<!-- existing <nav> gets responsive + overlay behaviour -->
<nav
  id="mobile-nav"
  data-mobile-nav-overlay
  (document:keydown.escape)="closeMobileNav()"
  class="fixed inset-y-0 start-0 z-30 flex w-72 max-w-[85vw] flex-col gap-1 border-e border-border-subtle bg-surface-low px-4 py-6 transition-transform duration-200 max-md:shadow-card md:static md:w-sidebar md:max-w-none md:shadow-none lg:shrink-0"
  [class.-translate-x-full]="!mobileNavOpen()"
  [class.md:translate-x-0]="true"
  [class.w-sidebar]="!collapsed()"
  [class.w-sidebar-collapsed]="collapsed()"
>
  <!-- existing brand + @for(items) + identity footer unchanged -->
  <!-- a close affordance inside the overlay, only shown on mobile -->
  <button
    type="button"
    (click)="closeMobileNav()"
    [attr.aria-label]="'sidebar.close' | t"
    class="absolute end-3 top-3 grid size-8 place-items-center rounded-full text-on-surface-variant hover:bg-surface-high lg:hidden"
  >
    <cs-icon name="close" [size]="20" />
  </button>
</nav>
<!-- a scrim behind the overlay on mobile -->
@if (mobileNavOpen()) {
  <div
    class="fixed inset-0 z-20 bg-black/40 md:hidden"
    (click)="closeMobileNav()"
    aria-hidden="true"
  ></div>
}
```

The portal shell mirrors this with its own `mobileNavOpen`/`toggleMobileNav`/`closeMobileNav` and the
same `data-mobile-nav-trigger` / `data-mobile-nav-overlay` hooks (portal's nav already uses
`w-sidebar`; add `max-md:fixed max-md:translate-x-full` toggled by `mobileNavOpen()`).

- [ ] **Step 5: Add the two new chrome keys to `translations.ts`**

```ts
  'sidebar.menu': { en: 'Open menu', ar: 'فتح القائمة' },
  'sidebar.close': { en: 'Close menu', ar: 'إغلاق القائمة' },
```

- [ ] **Step 6: Run test to verify it passes**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/layout/shell.component.spec.ts'`
Expected: PASS, 2/2 (admin); repeat for portal-app.

- [ ] **Step 7: Commit**

```bash
git add frontend/projects/admin-app/src/app/layout/shell.component.ts frontend/projects/admin-app/src/app/layout/shell.component.html frontend/projects/admin-app/src/app/layout/shell.component.spec.ts frontend/projects/portal-app/src/app/layout/shell.component.ts frontend/projects/portal-app/src/app/layout/shell.component.html frontend/projects/portal-app/src/app/layout/shell.component.spec.ts frontend/projects/common/src/lib/i18n/translations.ts
git commit -m "feat(responsive): mobile overlay menu in both shells (US-311 T1)"
```

---

### Task 2: No page overflow + form/table focus (`AC-311.1`, `AC-311.3`)

**Files:**
- Modify: `frontend/projects/common/src/styles/theme.css`
- Modify: `frontend/projects/admin-app/src/styles.css`
- Modify: `frontend/projects/portal-app/src/styles.css`
- Create: `frontend/e2e/responsive-layout.spec.ts`
- Modify: `frontend/projects/admin-app/src/app/features/organisation/branches.component.spec.ts` (extend)
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.ts` (forms: ensure `min-w-0` + stacked labels at `max-md`)

**Interfaces:**
- Consumes: existing `AsyncState` loading/empty/error states (already rendered).

- [ ] **Step 1: Write the failing e2e + component tests**

```ts
// frontend/e2e/responsive-layout.spec.ts  (Playwright)
import { expect, test } from '@playwright/test';

for (const width of [375, 768, 1440]) {
  test(`AC311_1_NoPageOverflowAt${width}`, async ({ page }) => {
    await page.setViewportSize({ width, height: 800 });
    await page.goto('/login');
    // sign in via the seeded admin, then land on the dashboard
    await page.fill('input[name="email"]', 'admin@cce-platform.com');
    await page.fill('input[name="password"]', 'Admin@123456');
    await page.click('button[type="submit"]');
    await page.waitForURL('**/dashboard');

    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
    expect(overflow, `horizontal overflow at ${width}px`).toBeLessThanOrEqual(0);
  });
}

test('AC311_2_MobileMenuIsKeyboardReachable', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 });
  await page.goto('/dashboard');
  await page.locator('[data-mobile-nav-trigger]').focus();
  await page.keyboard.press('Enter');
  const overlay = page.locator('[data-mobile-nav-overlay]');
  await expect(overlay).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(overlay).toBeHidden();
  await expect(page.locator('[data-mobile-nav-trigger]')).toBeFocused();
});

test('AC311_3_TableAndFormKeepFocusOrder', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 });
  await page.goto('/branches');
  // the create dialog must keep its labelled inputs in DOM order under 375px
  await page.locator('cs-button', { hasText: 'Add branch' }).click();
  const inputs = page.locator('cs-dialog cs-input-field input');
  await expect(inputs.nth(0)).toHaveAttribute('formcontrolname', 'name');
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx playwright test e2e/responsive-layout.spec.ts`
Expected: FAIL — `data-mobile-nav-trigger` missing / overflow present.

- [ ] **Step 3: Add responsive tokens and guard rules to `theme.css`**

```css
/* frontend/projects/common/src/styles/theme.css — appended */
/* US-311: the only sanctioned breakpoints. Mobile <768 (max-md), tablet 768–1024 (md,max-lg),
   desktop >1024 (lg). Components may only add `md:`/`lg:`/`max-md:` prefixes to these tokens. */
:root {
  --shell-breakpoint-mobile: 767px;
  --shell-breakpoint-desktop: 1024px;
}

/* The document itself must never scroll horizontally — any overflow is internal to a
   `overflow-x-auto` region, never the page. A regression here is caught by AC311_1. */
html,
body {
  max-width: 100%;
  overflow-x: hidden;
}
```

In each app `styles.css`, ensure the routed `<router-outlet>` host and feature `<section>` blocks
carry `min-w-0` so flex children can shrink:

```css
/* admin-app/src/styles.css + portal-app/src/styles.css */
main > * { min-width: 0; }
```

And in `branches.component.html` / `ticket-create.component.html` the `<form>` already stacks; add
`max-md:flex-col` so two-column forms collapse cleanly at 375px (no `left`/`right` alignment).

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx playwright test e2e/responsive-layout.spec.ts`
Expected: PASS, 5/5 (3 overflow widths + 2 interaction).

- [ ] **Step 5: Commit**

```bash
git add frontend/projects/common/src/styles/theme.css frontend/projects/admin-app/src/styles.css frontend/projects/portal-app/src/styles.css frontend/e2e/responsive-layout.spec.ts frontend/projects/admin-app/src/app/features/organisation/branches.component.html frontend/projects/admin-app/src/app/features/organisation/branches.component.spec.ts
git commit -m "feat(responsive): no page overflow + focus-order guards (US-311 T2)"
```

## Definition of done

`AC-311.1` (no overflow at 375/768/1440) covered by Task 2's Playwright specs. `AC-311.2` (keyboard
reachable mobile menu with honest `aria-expanded`/Escape/focus-return) covered by Task 1's component
specs + Task 2's e2e. `AC-311.3` (tables/forms keep focus order at 375px) covered by Task 2's e2e and
the `overflow-x-auto` structural rule. Full gate:

```powershell
cd frontend
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
npx playwright test e2e/responsive-layout.spec.ts
```
