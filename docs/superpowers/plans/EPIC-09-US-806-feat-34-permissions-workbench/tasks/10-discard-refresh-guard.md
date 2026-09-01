# Task 10 — Discard, Refresh and the unsaved-changes guard (US-806, AC-806.18…AC-806.20)

**Files:**
- Create: `frontend/projects/admin-app/src/app/features/admin/permissions-dirty.guard.ts`
- Create: `frontend/projects/admin-app/src/app/features/admin/permissions-dirty.guard.spec.ts`
- Modify: `frontend/projects/admin-app/src/app/app.routes.ts:124-128` (the `permissions` route)
- Modify: `frontend/projects/admin-app/src/app/features/admin/permissions.component.ts` (`discard`, `refresh`, `confirmLeave`)
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts` (seven new keys)
- Test: `frontend/projects/admin-app/src/app/features/admin/permissions.component.spec.ts` (modify)

**Interfaces:**
- Consumes: `ConfirmationService` (Task 05); `hasUnsavedChanges()`, `isDirty`, `changes`,
  `seedDraft`, `saveOutcome`, `load()` from Tasks 08 and 09; `CanDeactivateFn` from
  `@angular/router`. The codebase has **no `CanDeactivate` precedent** — `common/src/lib/auth/guards.ts`
  holds only `CanActivateFn`s (`authGuard` at `:9`, `roleGuard` at `:33`) — so this guard sets the
  pattern and lives beside the one screen that needs it rather than in `common`.
- Produces:
  - `export interface UnsavedChangesHost { hasUnsavedChanges(): boolean; confirmLeave(): Observable<boolean> }`
  - `export const permissionsDirtyGuard: CanDeactivateFn<UnsavedChangesHost>`
  - On the component: `confirmLeave(): Observable<boolean>`

**Why the guard takes an interface, not the component.** `app.routes.ts:127` lazy-loads the
component (`loadComponent: () => import('./features/admin/permissions.component')`). A guard that
imported the component class would pull that chunk into the routes bundle and undo the lazy load. A
structural interface keeps the guard type-safe and the chunk lazy.

**`beforeunload` is already wired** — Task 08 added the host binding
(`host: { '(window:beforeunload)': 'onBeforeUnload($event)' }`). This task covers the in-app half,
which is the one that can show a real, translated dialog (spec `A9`).

## Steps

- [ ] **Step 1: Add the translation keys**

```ts
  'permissions.discardConfirm.title': { en: 'Discard {0} unsaved changes?', ar: 'تجاهل {0} تغييرات غير محفوظة؟' },
  'permissions.discardConfirm.body': {
    en: 'The matrix returns to the permissions currently stored on the server.',
    ar: 'ستعود المصفوفة إلى الصلاحيات المحفوظة حاليًا على الخادم.',
  },
  'permissions.discardConfirm.confirm': { en: 'Discard changes', ar: 'تجاهل التغييرات' },
  'permissions.leaveConfirm.title': { en: 'Leave with {0} unsaved changes?', ar: 'الخروج مع {0} تغييرات غير محفوظة؟' },
  'permissions.leaveConfirm.body': {
    en: 'These permission changes have not been saved and will be lost.',
    ar: 'لم يتم حفظ تغييرات الصلاحيات هذه وسيتم فقدانها.',
  },
  'permissions.leaveConfirm.confirm': { en: 'Leave without saving', ar: 'الخروج دون حفظ' },
  'permissions.refreshConfirm.title': { en: 'Reload and lose {0} unsaved changes?', ar: 'إعادة التحميل وفقدان {0} تغييرات غير محفوظة؟' },
```

`permissions.refreshConfirm` reuses `permissions.leaveConfirm.body` for its message — identical
meaning, and a second string to keep in sync would drift.

- [ ] **Step 2: Write the failing guard tests**

Create `permissions-dirty.guard.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { ConfirmationService } from 'common';
import { Observable, of } from 'rxjs';
import { permissionsDirtyGuard, UnsavedChangesHost } from './permissions-dirty.guard';

describe('permissionsDirtyGuard', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [ConfirmationService] }));

  function run(host: UnsavedChangesHost): boolean | Observable<boolean> {
    const snapshot = {} as ActivatedRouteSnapshot;
    const state = {} as RouterStateSnapshot;
    return TestBed.runInInjectionContext(
      () => permissionsDirtyGuard(host, snapshot, state, state) as boolean | Observable<boolean>,
    );
  }

  it('AC806_19_LeavingACleanScreenIsNotInterrupted', () => {
    const host: UnsavedChangesHost = {
      hasUnsavedChanges: () => false,
      confirmLeave: () => of(true),
    };

    expect(run(host)).toBe(true);
    expect(TestBed.inject(ConfirmationService).current()).toBeNull();
  });

  it('AC806_19_LeavingADirtyScreenAsksAndRespectsNo', () => {
    let asked = 0;
    const host: UnsavedChangesHost = {
      hasUnsavedChanges: () => true,
      confirmLeave: () => {
        asked += 1;
        return of(false);
      },
    };

    const result = run(host) as Observable<boolean>;
    let allowed: boolean | null = null;
    result.subscribe((value) => (allowed = value));

    expect(asked).toBe(1);
    expect(allowed).toBe(false);
  });

  it('AC806_19_LeavingADirtyScreenRespectsYes', () => {
    const host: UnsavedChangesHost = {
      hasUnsavedChanges: () => true,
      confirmLeave: () => of(true),
    };

    let allowed: boolean | null = null;
    (run(host) as Observable<boolean>).subscribe((value) => (allowed = value));

    expect(allowed).toBe(true);
  });
});
```

- [ ] **Step 3: Write the failing component tests**

Append to `permissions.component.spec.ts`:

```ts
  it('AC806_18_DiscardConfirmsAndCancellingKeepsTheDraft', () => {
    const fixture = render();
    flushList(fixture);

    checkboxes(fixture)[1].click();
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-discard"] button')!
      .click();
    fixture.detectChanges();

    const confirmations = TestBed.inject(ConfirmationService);
    expect(confirmations.current()?.title).toContain('Discard');

    confirmations.resolve(false);
    fixture.detectChanges();

    expect(checkboxes(fixture)[1].checked).toBe(true);
    http.expectNone(() => true);
  });

  it('AC806_18_DiscardAcceptedResetsToTheServerState', () => {
    const fixture = render();
    flushList(fixture);

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    fixture.componentInstance.discard();
    fixture.detectChanges();
    TestBed.inject(ConfirmationService).resolve(true);
    fixture.detectChanges();

    expect(checkboxes(fixture)[1].checked).toBe(false);
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-action-bar"]')).toBeNull();
    // Discard is local — it never re-reads from the server.
    http.expectNone(() => true);
  });

  it('AC806_20_RefreshConfirmsOnlyWhenDirty', () => {
    const fixture = render();
    flushList(fixture);

    // Clean: reloads straight away, no dialog.
    fixture.componentInstance.refresh();
    fixture.detectChanges();
    expect(TestBed.inject(ConfirmationService).current()).toBeNull();
    flushList(fixture);

    // Dirty: asks first, and declining leaves the draft and issues no request.
    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    fixture.componentInstance.refresh();
    fixture.detectChanges();

    const confirmations = TestBed.inject(ConfirmationService);
    expect(confirmations.current()).not.toBeNull();
    confirmations.resolve(false);
    fixture.detectChanges();

    http.expectNone(() => true);
    expect(checkboxes(fixture)[1].checked).toBe(true);
  });

  it('AC806_19_ConfirmLeaveAsksWhenDirty', () => {
    const fixture = render();
    flushList(fixture);

    checkboxes(fixture)[1].click();
    fixture.detectChanges();

    let allowed: boolean | null = null;
    fixture.componentInstance.confirmLeave().subscribe((value) => (allowed = value));
    fixture.detectChanges();

    const confirmations = TestBed.inject(ConfirmationService);
    expect(confirmations.current()?.danger).toBe(true);
    confirmations.resolve(true);

    expect(allowed).toBe(true);
  });
```

- [ ] **Step 4: Run them to verify they fail**

```bash
cd frontend && npx ng test admin-app --watch=false --include='**/permissions*.spec.ts'
```

Expected: guard file missing; `confirmLeave` undefined; discard and refresh apply without asking.

- [ ] **Step 5: Write the guard**

Create `permissions-dirty.guard.ts`:

```ts
import { CanDeactivateFn } from '@angular/router';
import { Observable } from 'rxjs';

/**
 * What the guard needs from a screen, expressed structurally so the guard does not import the
 * component and pull its lazy chunk into the routes bundle (`app.routes.ts:127`).
 */
export interface UnsavedChangesHost {
  hasUnsavedChanges(): boolean;
  confirmLeave(): Observable<boolean>;
}

/**
 * AC-806.19 — leaving the permission workbench with staged changes asks first.
 *
 * The screen owns the question (it knows how many changes and in whose language); the guard only
 * decides whether to ask. This is the codebase's first `CanDeactivateFn` — `common`'s guards
 * (`auth/guards.ts`) are all `CanActivateFn` — and it deliberately lives beside the one screen that
 * needs it rather than in the shared library.
 */
export const permissionsDirtyGuard: CanDeactivateFn<UnsavedChangesHost> = (component) =>
  component.hasUnsavedChanges() ? component.confirmLeave() : true;
```

- [ ] **Step 6: Register it on the route**

In `app.routes.ts`, the `permissions` entry (currently `:124-128`):

```ts
      {
        path: 'permissions',
        canActivate: [roleGuard('Admin')],
        // AC-806.19 — staged permission changes are not silently dropped by navigating away.
        canDeactivate: [permissionsDirtyGuard],
        loadComponent: () => import('./features/admin/permissions.component'),
      },
```

with the import at the top of the file:

```ts
import { permissionsDirtyGuard } from './features/admin/permissions-dirty.guard';
```

- [ ] **Step 7: Add the three methods to the component**

Replace `discard()` and `refresh()` from Task 08, and add `confirmLeave()`:

```ts
  /** AC-806.20 — a reload throws staged work away, so it asks when there is any. */
  refresh(): void {
    if (!this.isDirty()) {
      this.load();
      return;
    }

    this.confirmations
      .confirm({
        title: this.locale.t('permissions.refreshConfirm.title', this.changes().length),
        message: this.locale.t('permissions.leaveConfirm.body'),
        confirmText: this.locale.t('action.refresh'),
        cancelText: this.locale.t('action.cancel'),
        danger: true,
      })
      .subscribe((accepted) => {
        if (accepted) {
          this.saveOutcome.set(null);
          this.load();
        }
      });
  }

  /** AC-806.18 — local reset, confirmed. Never re-reads from the server. */
  discard(): void {
    const model = this.data();
    if (!model || !this.isDirty()) {
      return;
    }

    this.confirmations
      .confirm({
        title: this.locale.t('permissions.discardConfirm.title', this.changes().length),
        message: this.locale.t('permissions.discardConfirm.body'),
        confirmText: this.locale.t('permissions.discardConfirm.confirm'),
        cancelText: this.locale.t('action.cancel'),
        danger: true,
      })
      .subscribe((accepted) => {
        if (!accepted) {
          return;
        }
        this.saveOutcome.set(null);
        this.draft.set(this.seedDraft(model, new Map()));
      });
  }

  /**
   * AC-806.19 — asked by `permissionsDirtyGuard`. Returns the dialog's answer directly: `true`
   * leaves, `false` stays with the draft intact.
   */
  confirmLeave(): Observable<boolean> {
    return this.confirmations.confirm({
      title: this.locale.t('permissions.leaveConfirm.title', this.changes().length),
      message: this.locale.t('permissions.leaveConfirm.body'),
      confirmText: this.locale.t('permissions.leaveConfirm.confirm'),
      cancelText: this.locale.t('action.cancel'),
      danger: true,
    });
  }
```

Add `Observable` to the `rxjs` import at the top of the component.

- [ ] **Step 8: Run the tests to verify they pass**

```bash
cd frontend && npx ng test admin-app --watch=false --include='**/permissions*.spec.ts'
```

Expected: PASS — 3 guard tests plus the component file's full set. Paste the output below.

- [ ] **Step 9: Check the route wiring is still lazy**

```bash
cd frontend && npx ng build admin-app
```

Expected: `Build succeeded`, and the permissions component still appears as its **own lazy chunk** in
the build output. If it has been folded into the main bundle, the guard is importing the component
somewhere — fix the import, not the assertion.

- [ ] **Step 10: Also check `app.routes.spec.ts`**

```bash
cd frontend && npx ng test admin-app --watch=false --include='**/app.routes.spec.ts'
```

`app.routes.spec.ts` asserts the shape of the route table (it references `permissions` — see
`app.routes.spec.ts` for what exactly). Adding `canDeactivate` may need that expectation updated;
update the spec to match the intended table, not the other way round.

- [ ] **Step 11: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/admin/permissions-dirty.guard.ts \
        frontend/projects/admin-app/src/app/features/admin/permissions-dirty.guard.spec.ts \
        frontend/projects/admin-app/src/app/app.routes.ts \
        frontend/projects/admin-app/src/app/features/admin/permissions.component.ts \
        frontend/projects/admin-app/src/app/features/admin/permissions.component.spec.ts \
        frontend/projects/common/src/lib/i18n/translations.ts
git commit -m "feat: confirm discard, refresh and navigation away from staged permission changes (AC-806.18..AC-806.20)"
```

## Criteria covered

`AC-806.18`, `AC-806.19`, `AC-806.20`.

## Test evidence

Implemented 2026-09-01, in the same commit as Tasks 08, 09, 11 and 12:

```
npx ng test admin-app --watch=false --include='**/permissions*.spec.ts'
Test Files  2 passed (2)
     Tests  31 passed (31)
```

`permissions-dirty.guard.spec.ts` (3 tests: clean-screen no-op, dirty-and-decline, dirty-and-accept)
passes as its own file within that run. `npx ng build admin-app` confirms `permissions-component`
still code-splits as a separate lazy chunk (19.03 kB) — the guard's `import type` did not pull it
into the eagerly-loaded routes bundle.

## Deviations from the plan

1. **Merged into one commit with Tasks 08, 09, 11, 12** — see Task 08's evidence entry.
2. **The guard needed an explicit re-export.** The plan wrote `import type { UnsavedChangesHost }
   from './permissions.component'` in the guard file and had the guard's own spec import
   `UnsavedChangesHost` from `./permissions-dirty.guard'` — but a type-only import does not
   re-export by itself (TS2459). Added `export type { UnsavedChangesHost };` to
   `permissions-dirty.guard.ts` immediately after the import.
