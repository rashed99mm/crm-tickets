# Task 09 — Report exactly what did and did not save (US-806, AC-806.15…AC-806.17)

**Files:**
- Modify: `frontend/projects/admin-app/src/app/features/admin/permissions.component.ts` (Task 08's `apply()`, plus new state)
- Modify: `frontend/projects/admin-app/src/app/features/admin/permissions.component.html` (outcome banner)
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts` (four new keys)
- Test: `frontend/projects/admin-app/src/app/features/admin/permissions.component.spec.ts` (modify — **rewrites `AC805_4_CannotRemoveLastPermission`**)

**Interfaces:**
- Consumes: everything Task 08 produced — `apply()`, `retainOf()`, `load(retain)`, `draft`,
  `dirtyRoleIds`, `describe()`; `ApiError.code` / `ApiError.message_` (`api-error.ts:13-23`); the
  wire codes the backend plan fixes: `ERR087` (stale, Task 01) and the pre-existing `ERR002`
  (built-in floor).
- Produces (Tasks 10–12 rely on these):
  - `readonly saveOutcome: Signal<SaveOutcome | null>` where
    `interface SaveOutcome { saved: number; total: number; failures: readonly RoleSaveFailure[] }`
  - `interface RoleSaveFailure { roleId: string; roleName: string; code: string; message: string }`
  - `readonly staleRoleIds: Signal<readonly string[]>`
  - `reloadStale(): void`

**Why per-role atomicity needs this task.** The endpoint is atomic per role, not across roles (spec
`A3`). A save spanning three roles can therefore land two and refuse one, and the only dishonest
options are pretending it all worked or pretending none of it did. This task makes the screen say
`2 of 3 roles saved`, name the refused role with the server's own reason, and keep that role's staged
intent so the administrator can retry or amend it — while every other role shows reloaded server
truth.

**This task rewrites `AC805_4_CannotRemoveLastPermission`** (`permissions.component.spec.ts:118`).
It clicks a checkbox and expects an immediate `DELETE` with `ERR002`. The built-in-role rule is
unchanged and still enforced (`PermissionAdministrationService.cs:91`); what changes is that the
refusal now arrives from a `PUT` during save. `AC-805.4` keeps its integration coverage at
`Integration/PermissionTests.cs:72`.

## Steps

- [ ] **Step 1: Add the translation keys**

```ts
  'permissions.savePartial': { en: '{0} of {1} roles saved.', ar: 'تم حفظ {0} من {1} أدوار.' },
  'permissions.staleRole': {
    en: 'Someone else changed this role while you were editing. Reload to see the current permissions, then try again.',
    ar: 'قام مستخدم آخر بتعديل هذا الدور أثناء تحريرك له. أعد التحميل لعرض الصلاحيات الحالية ثم حاول مرة أخرى.',
  },
  'permissions.reload': { en: 'Reload', ar: 'إعادة التحميل' },
  'permissions.retained': {
    en: 'These changes are still staged and were not saved.',
    ar: 'لا تزال هذه التغييرات مُجهَّزة ولم يتم حفظها.',
  },
```

`permissions.lastRequired` and `permissions.mutationError` already exist (`translations.ts:1027-1031`)
and are reused verbatim — the built-in-role copy was already written for this exact refusal.

- [ ] **Step 2: Write the failing tests**

In `permissions.component.spec.ts`, **replace** `AC805_4_CannotRemoveLastPermission` with the three
tests below. They reuse `MODEL_TWO_ROLES`, `checkboxes()` and the `failure()` helper from Task 08;
add a `failure()` helper matching `users.component.spec.ts:11-22` if this file does not have one that
takes a status.

```ts
  function saveWith(fixture: ComponentFixture<PermissionsComponent>): void {
    fixture.componentInstance.save();
    fixture.detectChanges();
    TestBed.inject(ConfirmationService).resolve(true);
    fixture.detectChanges();
  }

  it('AC806_15_PartialFailureNamesWhatSavedAndWhatDidNot', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    checkboxes(fixture)[1].click();          // role-1 grant
    fixture.detectChanges();
    checkboxes(fixture)[5].click();          // role-2 grant
    fixture.detectChanges();
    saveWith(fixture);

    http.expectOne('/api/admin/permissions/role-1').flush(envelope(null));
    fixture.detectChanges();
    http.expectOne('/api/admin/permissions/role-2').flush(
      failure('ERR002', 'The last required permission cannot be removed from a built-in role.'),
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();

    // Server truth is reloaded for the role that saved; the refused role's intent is re-overlaid.
    flushList(fixture, MODEL_TWO_ROLES);

    const banner = (fixture.nativeElement as HTMLElement)
      .querySelector('[data-testid="permissions-save-outcome"]')!;
    expect(banner).not.toBeNull();
    expect(banner.textContent).toContain('1 of 2 roles saved.');
    expect(banner.textContent).toContain('Agent');
    expect(banner.textContent).toContain('A built-in role must keep at least one permission.');

    // role-2's staged change survived, role-1's is gone (it saved).
    expect(checkboxes(fixture)[5].checked).toBe(true);
    expect(text(fixture)).toContain('1 unsaved changes');
  });

  it('AC806_17_BuiltInRoleRefusalKeepsTheStagedChange', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    checkboxes(fixture)[0].click();          // revoke role-1's only permission
    fixture.detectChanges();
    saveWith(fixture);

    http.expectOne('/api/admin/permissions/role-1').flush(
      failure('ERR002', 'The last required permission cannot be removed from a built-in role.'),
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();
    flushList(fixture, MODEL_TWO_ROLES);

    const body = text(fixture);
    expect(body).toContain('A built-in role must keep at least one permission.');
    expect(body).not.toContain('Permission changes saved.');
    expect(checkboxes(fixture)[0].checked).toBe(false, 'the revoke is still staged');
    // No stale reload affordance — this refusal is not a concurrency problem.
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-reload"]')).toBeNull();
  });

  it('AC806_16_StaleRefusalOffersAReloadThatDropsThatRolesDraft', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    saveWith(fixture);

    http.expectOne('/api/admin/permissions/role-1').flush(
      failure('ERR087', 'This role\'s permissions were changed by someone else.'),
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();
    flushList(fixture, MODEL_TWO_ROLES);

    expect(text(fixture)).toContain('Someone else changed this role');
    expect(checkboxes(fixture)[1].checked).toBe(true, 'the intent is retained until the admin decides');

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-reload"] button')!
      .click();
    fixture.detectChanges();

    // Reload drops the stale role's draft and shows server state.
    flushList(fixture, MODEL_TWO_ROLES);
    expect(checkboxes(fixture)[1].checked).toBe(false);
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-action-bar"]')).toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-save-outcome"]')).toBeNull();
  });
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
cd frontend && npx ng test admin-app --watch=false --include='**/permissions.component.spec.ts'
```

Expected: the three new tests fail — no outcome banner, no reload control.

- [ ] **Step 4: Add the outcome state to the component**

Above the class, beside `PermissionChange`:

```ts
/** One role whose atomic save was refused, with the server's own reason. */
export interface RoleSaveFailure {
  readonly roleId: string;
  readonly roleName: string;
  readonly code: string;
  readonly message: string;
}

/** The result of one save across n dirty roles. Atomic per role, so partial outcomes are real. */
export interface SaveOutcome {
  readonly saved: number;
  readonly total: number;
  readonly failures: readonly RoleSaveFailure[];
}
```

In the class:

```ts
  readonly saveOutcome = signal<SaveOutcome | null>(null);

  /** Roles refused with ERR087 — the only failure a reload can resolve. */
  readonly staleRoleIds = computed<readonly string[]>(() =>
    this.saveOutcome()?.failures.filter((failure) => failure.code === 'ERR087').map((failure) => failure.roleId) ?? [],
  );
```

- [ ] **Step 5: Replace `apply()`'s result handling**

Swap the `if (!failures.length) { … }` block written in Task 08 for:

```ts
      .subscribe((results) => {
        this.saving.set(false);
        const failures = results
          .filter((result) => result.error !== null)
          .map<RoleSaveFailure>((result) => ({
            roleId: result.role.id,
            roleName: result.role.name,
            code: result.error!.code,
            message: this.failureMessage(result.error!),
          }));

        if (!failures.length) {
          this.saveOutcome.set(null);
          this.toast.success(this.locale.t('permissions.saveSuccess'));
          this.load();
          return;
        }

        this.saveOutcome.set({
          saved: results.length - failures.length,
          total: results.length,
          failures,
        });
        this.toast.error(
          this.locale.t('error.generic.title'),
          this.locale.t('permissions.savePartial', results.length - failures.length, results.length),
        );

        // Reload so every role that DID save shows server truth (spec A7), then re-overlay the
        // refused roles' intent so nothing the administrator asked for is silently discarded.
        this.load(this.retainOf(failures.map((failure) => failure.roleId), draft));
      });
```

and add:

```ts
  /**
   * The server's refusal, in the administrator's language. `ERR002` and `ERR087` are the two the
   * screen can explain; anything else gets the generic message rather than a raw server string,
   * which may be an unlocalised internal detail.
   */
  private failureMessage(error: ApiError): string {
    switch (error.code) {
      case 'ERR002':
        return this.locale.t('permissions.lastRequired');
      case 'ERR087':
        return this.locale.t('permissions.staleRole');
      default:
        return this.locale.t('permissions.mutationError');
    }
  }

  /** AC-806.16 — drops only the stale roles' drafts; other refused roles keep theirs. */
  reloadStale(): void {
    const stale = new Set(this.staleRoleIds());
    const retain = this.retainOf(
      this.dirtyRoleIds().filter((roleId) => !stale.has(roleId)),
      this.draft(),
    );
    this.saveOutcome.set(null);
    this.load(retain);
  }
```

Also clear the banner where a new attempt or a reset begins — at the top of `apply()`:

```ts
    this.saveOutcome.set(null);
```

and in `discard()`, before reseeding the draft.

- [ ] **Step 6: Add the banner to the template**

Immediately after the `<header>` block in `permissions.component.html`, replacing the `sr-only`
paragraphs that Task 08 removed:

```html
  @if (saveOutcome(); as outcome) {
    <div
      data-testid="permissions-save-outcome"
      role="alert"
      class="flex flex-col gap-3 rounded-xl border border-error bg-error-container px-4 py-3 text-on-error-container"
    >
      <p class="text-label-lg font-semibold">
        {{ 'permissions.savePartial' | t: outcome.saved : outcome.total }}
      </p>
      <ul class="flex flex-col gap-1 text-body-sm">
        @for (failure of outcome.failures; track failure.roleId) {
          <li><span class="font-semibold">{{ failure.roleName }}</span> — {{ failure.message }}</li>
        }
      </ul>
      <p class="text-body-sm">{{ 'permissions.retained' | t }}</p>
      @if (staleRoleIds().length) {
        <span data-testid="permissions-reload" class="self-start">
          <cs-button variant="secondary" type="button" (pressed)="reloadStale()">
            <cs-icon name="refresh" [size]="18" />
            {{ 'permissions.reload' | t }}
          </cs-button>
        </span>
      }
    </div>
  }
```

- [ ] **Step 7: Run the tests to verify they pass**

```bash
cd frontend && npx ng test admin-app --watch=false --include='**/permissions.component.spec.ts'
```

Expected: PASS — every test in the file, including Task 08's and the two surviving `AC805_1_*`
tests. Paste the output below.

- [ ] **Step 8: Build and run both suites**

```bash
cd frontend && npx ng build admin-app && npx ng test admin-app --watch=false && npx ng test common --watch=false
```

- [ ] **Step 9: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/admin/permissions.component.ts \
        frontend/projects/admin-app/src/app/features/admin/permissions.component.html \
        frontend/projects/admin-app/src/app/features/admin/permissions.component.spec.ts \
        frontend/projects/common/src/lib/i18n/translations.ts
git commit -m "feat: report partial, stale and built-in-role save refusals precisely (AC-806.15..AC-806.17)"
```

## Criteria covered

`AC-806.15`, `AC-806.16`, `AC-806.17`. `AC-805.4`'s frontend assertion is retired in favour of the
new refusal path; the criterion keeps integration coverage at `Integration/PermissionTests.cs:72`.

## Test evidence

Implemented 2026-09-01, in the same commit as Tasks 08, 10, 11 and 12 (see deviation note below).
`AC-806.15`, `AC-806.16`, `AC-806.17` are each covered by a named test in
`permissions.component.spec.ts`:

```
npx ng test admin-app --watch=false --include='**/permissions*.spec.ts'
Test Files  2 passed (2)
     Tests  31 passed (31)
```

`AC806_15_PartialFailureNamesWhatSavedAndWhatDidNot`,
`AC806_17_BuiltInRoleRefusalKeepsTheStagedChange` and
`AC806_16_StaleRefusalOffersAReloadThatDropsThatRolesDraft` all pass. `AC-805.4`'s frontend
assertion (the old `AC805_4_CannotRemoveLastPermission` test, which asserted an immediate `DELETE`)
is retired; the criterion keeps integration coverage at `Integration/PermissionTests.cs:72` (itself
currently blocked — see Task 03).

## Deviations from the plan

1. **Merged into one commit with Tasks 08, 10, 11, 12** — see Task 08's evidence entry for the
   rationale.
2. **One test assertion was wrong, not the code**: the plan's `AC806_13` test asserted
   `text(fixture)).toContain('Permission changes saved.')`. That string is a `ToastService` toast,
   rendered by `CsToastHost` in the app shell — a component this isolated fixture does not mount.
   Fixed by asserting on the observable state (the action bar and outcome banner both cleared)
   instead of toast text that was never going to appear in this fixture.
