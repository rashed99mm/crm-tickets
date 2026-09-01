# Task 07 — Confirm the three unconfirmed destructive actions (US-807, AC-807.5…AC-807.7)

**Files:**
- Modify: `frontend/projects/admin-app/src/app/features/users/users.component.ts:244-250` (`toggleActive`) and its import block (`:1-25`)
- Modify: `frontend/projects/admin-app/src/app/features/organisation/departments.component.ts:111-116` (`deactivate`) and its import block (`:3-21`)
- Modify: `frontend/projects/admin-app/src/app/features/organisation/sla-policies.component.ts:251-256` (`deactivate`) and its import block (`:3-24`)
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts` (six new keys, en + ar)
- Test: `frontend/projects/admin-app/src/app/features/users/users.component.spec.ts` (modify)
- Test: `frontend/projects/admin-app/src/app/features/organisation/departments.component.spec.ts` (**create** — no spec file exists for this screen)
- Test: `frontend/projects/admin-app/src/app/features/organisation/sla-policies.component.spec.ts` (modify)

**Interfaces:**
- Consumes: `ConfirmationService.confirm(...)` with the FIFO queue from Task 05, used exactly as
  `kb-admin.component.ts:335-352` already does; `LocaleStore.t(key, ...params)`
  (`locale.store.ts:74`), already injected in all three components.
- Produces: nothing later tasks depend on. This task is the low-risk proof that Task 05's queue
  works in a real screen before the permissions workbench relies on it.

**Current code — all three fire immediately.**

```ts
// users.component.ts:244
  toggleActive(user: StaffUser): void {
    this.api.setActive(user.id, !user.isActive).subscribe({
      next: () => this.load(),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }
```

`departments.component.ts:111` and `sla-policies.component.ts:251` are the same three lines with a
different api call. The users one is the only one that is *conditionally* destructive: the same
button activates and deactivates (`users.component.html:220-222`), so the confirmation must be gated
on `user.isActive` or activating an account becomes needlessly obstructive (`AC-807.5`).

## Steps

- [ ] **Step 1: Add the translation keys**

In `frontend/projects/common/src/lib/i18n/translations.ts`, beside the existing `users.*` and
`departments.*` blocks (the `permissions.*` block sits at `:1016-1035` for reference on formatting):

```ts
  'users.deactivateConfirm.title': { en: 'Deactivate this account?', ar: 'إلغاء تنشيط هذا الحساب؟' },
  'users.deactivateConfirm.body': {
    en: '{0} will be signed out and will not be able to sign in again until the account is reactivated.',
    ar: 'سيتم تسجيل خروج {0} ولن يتمكن من تسجيل الدخول حتى يتم إعادة تنشيط الحساب.',
  },
  'users.deactivateConfirm.confirm': { en: 'Deactivate', ar: 'إلغاء التنشيط' },

  'departments.deactivateConfirm.title': { en: 'Deactivate this department?', ar: 'إلغاء تنشيط هذا القسم؟' },
  'departments.deactivateConfirm.body': {
    en: '{0} will stop appearing in assignment and routing choices. Existing tickets keep their department.',
    ar: 'لن يظهر {0} في خيارات التعيين والتوجيه. تحتفظ التذاكر الحالية بقسمها.',
  },

  'slaPolicies.deactivateConfirm.title': { en: 'Deactivate this SLA policy?', ar: 'إلغاء تنشيط سياسة الخدمة؟' },
  'slaPolicies.deactivateConfirm.body': {
    en: '{0} will stop applying to new tickets. Tickets already tracking against it keep their targets.',
    ar: 'لن تُطبق {0} على التذاكر الجديدة. تحتفظ التذاكر الحالية بأهدافها.',
  },
```

Reuse `departments.deactivate` and `action.cancel` for the buttons — both exist already
(`departments.component.html:83`, and `action.cancel` is the host's default at
`confirmation-host.component.html`).

- [ ] **Step 2: Write the failing test for users**

Append to `users.component.spec.ts`. The file's helpers (`envelope`, `isUsersList`, the `STAFF`
fixture at `:25-47`) already exist; `STAFF[0]` is active and `STAFF[1]` is not, which is exactly the
pair this criterion needs:

```ts
  it('AC807_5_DeactivateConfirmsBeforeSending: cancelling issues no request', async () => {
    const fixture = render();
    flushList(fixture);

    const confirmations = TestBed.inject(ConfirmationService);
    const row = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr')[0];
    row.querySelector<HTMLButtonElement>('button')!.click();
    fixture.detectChanges();

    // Nothing sent yet — the dialog is the gate.
    http.expectNone((request) => request.method === 'PUT');
    expect(confirmations.current()).not.toBeNull();
    expect(confirmations.current()?.danger).toBe(true);

    confirmations.resolve(false);
    fixture.detectChanges();

    http.expectNone((request) => request.method === 'PUT');
  });

  it('AC807_5_DeactivateSendsAfterConfirming', async () => {
    const fixture = render();
    flushList(fixture);

    const confirmations = TestBed.inject(ConfirmationService);
    const row = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr')[0];
    row.querySelector<HTMLButtonElement>('button')!.click();
    fixture.detectChanges();

    confirmations.resolve(true);
    fixture.detectChanges();

    const request = http.expectOne('/api/Users/1/deactivate');
    expect(request.request.method).toBe('PUT');
    request.flush(envelope(null));
  });

  it('AC807_5_ActivatingDoesNotConfirm: only the destructive direction is gated', () => {
    const fixture = render();
    flushList(fixture);

    const confirmations = TestBed.inject(ConfirmationService);
    // STAFF[1] is inactive, so this button activates.
    const row = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr')[1];
    row.querySelector<HTMLButtonElement>('button')!.click();
    fixture.detectChanges();

    expect(confirmations.current()).toBeNull();
    http.expectOne('/api/Users/2/activate').flush(envelope(null));
  });
```

Add `ConfirmationService` to the file's `common` import and `TestBed` to the Angular testing import
if not already present.

**Check the real URL before running.** `staff.api.ts:116` builds
`` `/api/Users/${id}/${action}` `` — confirm whether `action` is `activate`/`deactivate` or
`true`/`false` and use whatever the code actually produces. Do not change the api to match the test.

- [ ] **Step 3: Run it to verify it fails**

```bash
cd frontend && npx ng test admin-app --watch=false --include='**/users.component.spec.ts'
```

Expected: the first two fail — the `PUT` fires immediately, so `http.expectNone` throws.

- [ ] **Step 4: Gate the users action**

Add to the `common` import block in `users.component.ts`: `ConfirmationService`. Then:

```ts
  private readonly confirmations = inject(ConfirmationService);
```

```ts
  /**
   * AC-807.5 — deactivation signs the account out and is confirmed; activation is not destructive
   * and is not gated. One button serves both directions (users.component.html:220).
   */
  toggleActive(user: StaffUser): void {
    if (!user.isActive) {
      this.setActive(user, true);
      return;
    }

    this.confirmations
      .confirm({
        title: this.locale.t('users.deactivateConfirm.title'),
        message: this.locale.t('users.deactivateConfirm.body', `${user.firstName} ${user.lastName}`),
        confirmText: this.locale.t('users.deactivateConfirm.confirm'),
        cancelText: this.locale.t('action.cancel'),
        danger: true,
      })
      .subscribe((accepted) => {
        if (accepted) {
          this.setActive(user, false);
        }
      });
  }

  private setActive(user: StaffUser, isActive: boolean): void {
    this.api.setActive(user.id, isActive).subscribe({
      next: () => this.load(),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }
```

- [ ] **Step 5: Run it to verify it passes**

```bash
cd frontend && npx ng test admin-app --watch=false --include='**/users.component.spec.ts'
```

Expected: PASS, all tests in the file. Paste the output below.

- [ ] **Step 6: Commit the users half**

```bash
git add frontend/projects/common/src/lib/i18n/translations.ts \
        frontend/projects/admin-app/src/app/features/users/users.component.ts \
        frontend/projects/admin-app/src/app/features/users/users.component.spec.ts
git commit -m "feat: confirm before deactivating a staff account (AC-807.5)"
```

- [ ] **Step 7: Write the failing test for departments**

Create `departments.component.spec.ts`. There is no spec for this screen, so the fixture scaffold
comes from `sla-policies.component.spec.ts` (same shape, same `common` providers) — read it first and
mirror its `beforeEach`, its `envelope` helper and its list-flush helper rather than inventing a
second convention. Then:

```ts
  it('AC807_6_DeactivateConfirmsBeforeSending: cancelling issues no request', () => {
    const fixture = render();
    flushList(fixture);

    const confirmations = TestBed.inject(ConfirmationService);
    (fixture.nativeElement as HTMLElement)
      .querySelectorAll<HTMLButtonElement>('tbody tr button')[0]
      .click();
    fixture.detectChanges();

    expect(confirmations.current()?.danger).toBe(true);
    http.expectNone((request) => request.method === 'DELETE');

    confirmations.resolve(false);
    fixture.detectChanges();

    http.expectNone((request) => request.method === 'DELETE');
  });

  it('AC807_6_DeactivateSendsAfterConfirming', () => {
    const fixture = render();
    flushList(fixture);

    const confirmations = TestBed.inject(ConfirmationService);
    (fixture.nativeElement as HTMLElement)
      .querySelectorAll<HTMLButtonElement>('tbody tr button')[0]
      .click();
    fixture.detectChanges();
    confirmations.resolve(true);
    fixture.detectChanges();

    const request = http.expectOne((r) => r.method === 'DELETE');
    request.flush(envelope(null));
  });
```

Confirm the verb against `organisation.api.ts:45` — `deactivate` is `http.delete('/api/Departments/{id}')`
there, hence `DELETE`; if the api uses `PUT`, match the api.

- [ ] **Step 8: Gate the departments action**

Add `ConfirmationService` to the import block and inject it, then:

```ts
  /** AC-807.6 — deactivation removes the department from routing choices, so it confirms first. */
  deactivate(department: Department): void {
    this.confirmations
      .confirm({
        title: this.locale.t('departments.deactivateConfirm.title'),
        message: this.locale.t('departments.deactivateConfirm.body', department.name),
        confirmText: this.locale.t('departments.deactivate'),
        cancelText: this.locale.t('action.cancel'),
        danger: true,
      })
      .subscribe((accepted) => {
        if (!accepted) {
          return;
        }
        this.api.deactivate(department.id).subscribe({
          next: () => this.load(),
          error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
        });
      });
  }
```

- [ ] **Step 9: Repeat for SLA policies**

Same edit in `sla-policies.component.ts:251`, with `slaPolicies.deactivateConfirm.*` keys and
`policy.name`:

```ts
  /** AC-807.7 — a deactivated policy stops applying to new tickets, so it confirms first. */
  deactivate(policy: SLAPolicy): void {
    this.confirmations
      .confirm({
        title: this.locale.t('slaPolicies.deactivateConfirm.title'),
        message: this.locale.t('slaPolicies.deactivateConfirm.body', policy.name),
        confirmText: this.locale.t('departments.deactivate'),
        cancelText: this.locale.t('action.cancel'),
        danger: true,
      })
      .subscribe((accepted) => {
        if (!accepted) {
          return;
        }
        this.api.deactivate(policy.id).subscribe({
          next: () => this.load(),
          error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
        });
      });
  }
```

and the matching pair of tests in `sla-policies.component.spec.ts`, named
`AC807_7_DeactivateConfirmsBeforeSending` and `AC807_7_DeactivateSendsAfterConfirming`.

`departments.deactivate` is reused as the confirm button label in both because the SLA template
already reuses it (`sla-policies.component.html:286`) — adding a second key for identical copy would
be a translation to keep in sync for no reason.

- [ ] **Step 10: Run the admin-app suite**

```bash
cd frontend && npx ng test admin-app --watch=false
```

Expected: PASS across the suite, three screens newly gated. Paste the output below.

- [ ] **Step 11: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/organisation \
        frontend/projects/common/src/lib/i18n/translations.ts
git commit -m "feat: confirm before deactivating a department or SLA policy (AC-807.6, AC-807.7)"
```

## Criteria covered

`AC-807.5`, `AC-807.6`, `AC-807.7`.

## Deliberately not touched

`customer-detail.component.ts:225` keeps its bespoke inline confirm and `kb-admin.component.ts:335`
already uses the service (spec `A12`). Both are already gated; migrating them would churn passing
tests for no behaviour change.

## Test evidence

Implemented 2026-09-01:

```
npx ng test admin-app --watch=false --include='**/users.component.spec.ts' \
  --include='**/departments.component.spec.ts' --include='**/sla-policies.component.spec.ts'
Test Files  3 passed (3)
     Tests  24 passed (24)
```

`npx ng build admin-app`: succeeded (one pre-existing bundle-size budget warning, unrelated to this
task's files).

## Deviations from the plan

1. **`departments.component.spec.ts` needed a `createdAt` field.** The plan's fixture DEPARTMENT
   object omitted it; `Department` (`organisation.api.ts:7-12`) requires it. Added
   `createdAt: '2026-08-01T00:00:00Z'` to the fixture.
2. **`SLAPolicy` has no `name` field** (`sla-policy.api.ts:8-17`) — only `priority`,
   `responseTargetHours`, `resolutionTargetHours`. The plan's confirm-message design assumed a name;
   the message instead identifies the policy by its priority ("The {0} priority policy will stop
   applying…"), and the translation copy was written accordingly rather than as originally drafted.
3. **Tests call the component methods directly** (`fixture.componentInstance.deactivate(...)`)
   rather than clicking a table button, matching this file's own established convention
   (`sla-policies.component.spec.ts` already tests via `startEdit()`/`saveEdit()`, not DOM clicks).
