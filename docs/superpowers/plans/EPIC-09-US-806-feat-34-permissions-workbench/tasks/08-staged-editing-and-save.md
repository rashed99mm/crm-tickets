# Task 08 — Staged editing replaces instant apply (US-806, AC-806.11…AC-806.14, AC-806.24)

**Files:**
- Modify: `frontend/projects/common/src/lib/admin/permission.api.ts:22-37` (add `setRolePermissions`)
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts:1016-1035` (the `permissions.*` block)
- Modify: `frontend/projects/admin-app/src/app/features/admin/permissions.component.ts` (whole file — 115 lines today)
- Modify: `frontend/projects/admin-app/src/app/features/admin/permissions.component.html` (header actions, checkbox cells, new action bar)
- Test: `frontend/projects/admin-app/src/app/features/admin/permissions.component.spec.ts` (modify — **rewrites two existing tests**)

**Interfaces:**
- Consumes: `ConfirmationService.confirm({..., details})` (Tasks 05, 06); the existing
  `PermissionApi.list()` (`permission.api.ts:26-28`); `AsyncState` / `loading()` / `failed()`
  (`common/src/lib/state/async-state.ts:12-19`); `ToastService.success(title, message?)` /
  `.error(title, message?)` (`toast.service.ts:19-23`); `LocaleStore.t(key, ...params)`
  (`locale.store.ts:74`); the endpoint from backend Task 03.
- Produces (Tasks 09–12 rely on these exact names):
  - `PermissionApi.setRolePermissions(roleId: string, permissionIds: readonly string[], expectedPermissionIds: readonly string[]): Observable<unknown>`
  - `export interface PermissionChange { roleId; roleName; permissionId; permissionName; kind: 'grant' | 'revoke' }`
  - `type Draft = ReadonlyMap<string, ReadonlySet<string>>`
  - On the component: `draft`, `saving`, `changes`, `isDirty`, `dirtyRoleIds`, `isChecked(roleId, permissionId)`,
    `isStaged(roleId, permissionId)`, `toggle(role, permission, checked)`, `save()`, `discard()`,
    `load(retain?: Draft)`, `hasUnsavedChanges()`, `describe(change)`

**This task deliberately breaks two existing tests and fixes them in the same commit.**
`permissions.component.spec.ts:80` (`AC805_2_AssignPermissionToRole`) and `:101`
(`AC805_3_RevokePermissionFromRole`) assert that a checkbox click fires `POST`/`DELETE` immediately.
That is the contract being replaced (spec Finding 4). `AC-805.2`/`AC-805.3` keep their coverage at
integration level (`Integration/PermissionTests.cs:28`), where the two single-mapping endpoints are
still exercised and still work. Rewrite them here; do not delete them and do not defer them.

**Why `concat` and not `forkJoin`.** Saves run one role at a time. Parallel requests against the same
table would make the partial-failure report in Task 09 depend on interleaving, and the backend takes
an `UPDLOCK` per role — parallelism buys nothing and costs determinism.

## Steps

- [ ] **Step 1: Add the api method**

In `frontend/projects/common/src/lib/admin/permission.api.ts`, after `revoke` (line 34-36):

```ts
  /**
   * Replaces the role's whole permission set (AC-806.13). `expectedPermissionIds` is the set the UI
   * staged from — the server refuses the call if the stored set has moved since (AC-806.5), which is
   * what stops two administrators silently overwriting one another.
   */
  setRolePermissions(
    roleId: string,
    permissionIds: readonly string[],
    expectedPermissionIds: readonly string[],
  ): Observable<unknown> {
    return this.http.put(`/api/admin/permissions/${roleId}`, { permissionIds, expectedPermissionIds });
  }
```

- [ ] **Step 2: Add the translation keys**

In `translations.ts`, inside the `permissions.*` block (after `permissions.count` at `:1032-1035`):

```ts
  'permissions.pending': { en: '{0} unsaved changes', ar: '{0} تغييرات غير محفوظة' },
  'permissions.save': { en: 'Save {0} changes', ar: 'حفظ {0} تغييرات' },
  'permissions.discard': { en: 'Discard', ar: 'تجاهل' },
  'permissions.staged': { en: 'changed', ar: 'تم التغيير' },
  'permissions.stagedHint': {
    en: 'This change is staged and has not been saved yet.',
    ar: 'هذا التغيير مُجهَّز ولم يتم حفظه بعد.',
  },
  'permissions.saveConfirm.title': { en: 'Apply {0} permission changes?', ar: 'تطبيق {0} تغييرات على الصلاحيات؟' },
  'permissions.saveConfirm.body': {
    en: 'These take effect for every member of the affected roles as soon as they are saved.',
    ar: 'تسري هذه التغييرات على جميع أعضاء الأدوار المتأثرة بمجرد حفظها.',
  },
  'permissions.saveConfirm.confirm': { en: 'Apply changes', ar: 'تطبيق التغييرات' },
  'permissions.change.grant': { en: 'Grant {0} → {1}', ar: 'منح {0} ← {1}' },
  'permissions.change.revoke': { en: 'Revoke {0} → {1}', ar: 'إلغاء {0} ← {1}' },
  'permissions.saveSuccess': { en: 'Permission changes saved.', ar: 'تم حفظ تغييرات الصلاحيات.' },
```

The Arabic arrows point left because the sentence runs right-to-left — the mirrored glyph is
deliberate, not a copy-paste slip.

- [ ] **Step 3: Write the failing tests**

In `permissions.component.spec.ts`: add a two-role fixture beside the existing `MODEL` (leave `MODEL`
alone — the `AC805_1` tests assert against its exact shape), and **replace** the two legacy tests.

```ts
const MODEL_TWO_ROLES = {
  roles: [
    { id: 'role-1', name: 'Admin', permissionIds: ['permission-1'] },
    { id: 'role-2', name: 'Agent', permissionIds: ['permission-2'] },
  ],
  permissions: [
    { id: 'permission-1', name: 'ticket.view', description: 'View tickets' },
    { id: 'permission-2', name: 'ticket.close', description: 'Close tickets' },
    { id: 'permission-3', name: 'report.view', description: 'View reports' },
  ],
};

function checkboxes(fixture: ComponentFixture<PermissionsComponent>): HTMLInputElement[] {
  return Array.from(
    (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>('tbody input[type="checkbox"]'),
  );
}
```

```ts
  // Replaces AC805_2_AssignPermissionToRole — the endpoint it asserted is still covered by
  // Integration/PermissionTests.cs; what changed is that the screen no longer calls it per click.
  it('AC806_11_TogglingStagesWithoutSendingAnything', () => {
    const fixture = render();
    flushList(fixture);

    const box = checkboxes(fixture)[1];
    expect(box.checked).toBe(false);
    box.click();
    fixture.detectChanges();

    // Nothing at all reaches the network.
    http.expectNone(() => true);
    // The box reflects the draft, and the cell says so in words.
    expect(checkboxes(fixture)[1].checked).toBe(true);
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="staged-marker"]')).not.toBeNull();
    expect(text(fixture)).toContain('1 unsaved changes');
  });

  it('AC806_24_TheActionBarIsAbsentUntilSomethingIsStaged', () => {
    const fixture = render();
    flushList(fixture);

    const bar = () => (fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-action-bar"]');
    expect(bar()).toBeNull();

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    expect(bar()).not.toBeNull();

    // Toggling back to the loaded state is not a change, so the bar goes away again.
    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    expect(bar()).toBeNull();
  });

  it('AC806_24_SaveIsANoOpWhenNothingIsStaged', () => {
    const fixture = render();
    flushList(fixture);

    fixture.componentInstance.save();
    fixture.detectChanges();

    expect(TestBed.inject(ConfirmationService).current()).toBeNull();
    http.expectNone(() => true);
  });

  it('AC806_12_SaveConfirmsAndListsEveryStagedChange', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    // Grant permission-2 to Admin, revoke permission-2 from Agent.
    const boxes = checkboxes(fixture);
    boxes[1].click();   // Admin × ticket.close  → grant
    fixture.detectChanges();
    checkboxes(fixture)[4].click();   // Agent × ticket.close → revoke
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-save"] button')!
      .click();
    fixture.detectChanges();

    const request = TestBed.inject(ConfirmationService).current();
    expect(request).not.toBeNull();
    expect(request!.details).toEqual([
      'Grant ticket.close → Admin',
      'Revoke ticket.close → Agent',
    ]);
    // A revoke is destructive, so the dialog is the danger variant.
    expect(request!.danger).toBe(true);
    // And still nothing has been sent.
    http.expectNone(() => true);
  });

  it('AC806_14_CancellingTheDialogSendsNothingAndKeepsTheDraft', () => {
    const fixture = render();
    flushList(fixture);

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    fixture.componentInstance.save();
    fixture.detectChanges();

    TestBed.inject(ConfirmationService).resolve(false);
    fixture.detectChanges();

    http.expectNone(() => true);
    expect(checkboxes(fixture)[1].checked).toBe(true);
    expect(text(fixture)).toContain('1 unsaved changes');
  });

  // Replaces AC805_3_RevokePermissionFromRole.
  it('AC806_13_AcceptingSendsOnePutPerDirtyRoleThenReloads', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    checkboxes(fixture)[1].click();            // Admin gains ticket.close
    fixture.detectChanges();
    checkboxes(fixture)[3].click();            // Agent loses ticket.view? (index → role-2 × permission-1)
    fixture.detectChanges();
    fixture.componentInstance.save();
    fixture.detectChanges();

    TestBed.inject(ConfirmationService).resolve(true);
    fixture.detectChanges();

    const first = http.expectOne('/api/admin/permissions/role-1');
    expect(first.request.method).toBe('PUT');
    expect(first.request.body).toEqual({
      permissionIds: ['permission-1', 'permission-2'],
      expectedPermissionIds: ['permission-1'],
    });
    first.flush(envelope(null));
    fixture.detectChanges();

    // Sequential: role-2's request is only issued after role-1's completes.
    const second = http.expectOne('/api/admin/permissions/role-2');
    expect(second.request.method).toBe('PUT');
    second.flush(envelope(null));
    fixture.detectChanges();

    // Checked state follows the server, so a reload closes the flow (spec A7).
    flushList(fixture, MODEL_TWO_ROLES);
    expect(text(fixture)).toContain('Permission changes saved.');
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-action-bar"]')).toBeNull();
  });
```

**Before running, fix the checkbox indices.** They are ordinal over `tbody input[type="checkbox"]`,
so with `MODEL_TWO_ROLES` (2 roles × 3 permissions) the order is
`[r1p1, r1p2, r1p3, r2p1, r2p2, r2p3]`. Verify against the rendered DOM and correct the comments —
an index off by one here produces a test that passes for the wrong reason. Add `ConfirmationService`
to the `common` import in this spec file.

- [ ] **Step 4: Run the tests to verify they fail**

```bash
cd frontend && npx ng test admin-app --watch=false --include='**/permissions.component.spec.ts'
```

Expected: the new tests fail (no action bar, no staged marker, `save` undefined) **and** the two
legacy tests you replaced are gone. The two `AC805_1_*` tests and `AC805_4_*` must still pass — the
last one is about the `ERR002` message and is rewritten in Task 09, not here.

- [ ] **Step 5: Rewrite the component**

Replace `permissions.component.ts` in full:

```ts
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { catchError, concat, map, of, toArray } from 'rxjs';
import {
  ApiError,
  AsyncState,
  ConfirmationService,
  CsButton,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  failed,
  loading,
  LocaleStore,
  PermissionAdministration,
  PermissionAdministrationPermission,
  PermissionAdministrationRole,
  PermissionApi,
  ToastService,
  TranslatePipe,
} from 'common';

/** One staged difference between the loaded snapshot and the draft. */
export interface PermissionChange {
  readonly roleId: string;
  readonly roleName: string;
  readonly permissionId: string;
  readonly permissionName: string;
  readonly kind: 'grant' | 'revoke';
}

/** The permission ids each role would hold if the draft were saved. */
type Draft = ReadonlyMap<string, ReadonlySet<string>>;

/**
 * US-806 — the role permission workbench.
 *
 * The screen this replaces sent a `POST` or `DELETE` on every checkbox click, with no confirmation
 * and no undo: re-scoping a role meant eight requests and eight intermediate states that were each
 * briefly live. Here a click mutates a local draft, `changes()` diffs that draft against the loaded
 * snapshot, and saving sends one atomic `PUT` per dirty role after the administrator has seen the
 * list of what will change.
 *
 * Checked state still follows the server, never the click (spec A7): a successful save reloads.
 */
@Component({
  selector: 'admin-permissions',
  imports: [CsCard, CsButton, CsIcon, CsLoadingState, CsEmptyState, CsErrorState, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './permissions.component.html',
  host: { '(window:beforeunload)': 'onBeforeUnload($event)' },
})
export default class PermissionsComponent {
  private readonly api = inject(PermissionApi);
  private readonly toast = inject(ToastService);
  private readonly locale = inject(LocaleStore);
  private readonly confirmations = inject(ConfirmationService);

  readonly state = signal<AsyncState<PermissionAdministration>>(loading());
  readonly draft = signal<Draft>(new Map());
  readonly saving = signal(false);

  readonly data = computed(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : null;
  });

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  readonly summary = computed<{ roles: number; permissions: number } | null>(() => {
    const model = this.data();
    return model ? { roles: model.roles.length, permissions: model.permissions.length } : null;
  });

  /**
   * The staged diff. Grants first, then revokes, per role in load order — a stable order matters
   * because this list is what the confirmation dialog shows (AC-806.12).
   */
  readonly changes = computed<readonly PermissionChange[]>(() => {
    const model = this.data();
    const draft = this.draft();
    if (!model) {
      return [];
    }

    const names = new Map(model.permissions.map((permission) => [permission.id, permission.name]));
    const changes: PermissionChange[] = [];

    for (const role of model.roles) {
      const stored = new Set(role.permissionIds);
      const desired = draft.get(role.id) ?? stored;

      for (const permissionId of desired) {
        if (!stored.has(permissionId)) {
          changes.push({
            roleId: role.id,
            roleName: role.name,
            permissionId,
            permissionName: names.get(permissionId) ?? permissionId,
            kind: 'grant',
          });
        }
      }
      for (const permissionId of stored) {
        if (!desired.has(permissionId)) {
          changes.push({
            roleId: role.id,
            roleName: role.name,
            permissionId,
            permissionName: names.get(permissionId) ?? permissionId,
            kind: 'revoke',
          });
        }
      }
    }

    return changes;
  });

  readonly isDirty = computed(() => this.changes().length > 0);

  readonly dirtyRoleIds = computed<readonly string[]>(() => [
    ...new Set(this.changes().map((change) => change.roleId)),
  ]);

  constructor() {
    this.load();
  }

  /**
   * Reloads the matrix and reseeds the draft from it. `retain` re-overlays the desired sets of roles
   * whose save was refused, so a rejected role keeps the administrator's intent while every other
   * role shows server truth (AC-806.15).
   */
  load(retain: Draft = new Map()): void {
    this.state.set(loading());
    this.api.list().subscribe({
      next: (result) => {
        this.state.set(
          result.permissions.length ? { status: 'loaded', data: result } : { status: 'empty' },
        );
        this.draft.set(this.seedDraft(result, retain));
      },
      error: (error: unknown) => {
        const apiError = this.toApiError(error);
        this.state.set(failed(apiError));
        this.draft.set(new Map());
        this.toast.error(this.locale.t('error.generic.title'), this.locale.t('permissions.loadError'));
      },
    });
  }

  refresh(): void {
    this.load();
  }

  isChecked(roleId: string, permissionId: string): boolean {
    return this.draft().get(roleId)?.has(permissionId) ?? false;
  }

  /** True when the draft and the loaded snapshot disagree about this cell. */
  isStaged(roleId: string, permissionId: string): boolean {
    const role = this.data()?.roles.find((candidate) => candidate.id === roleId);
    if (!role) {
      return false;
    }
    return role.permissionIds.includes(permissionId) !== this.isChecked(roleId, permissionId);
  }

  toggle(
    role: PermissionAdministrationRole,
    permission: PermissionAdministrationPermission,
    checked: boolean,
  ): void {
    if (this.saving()) {
      return;
    }

    this.draft.update((draft) => {
      const next = new Map(draft);
      const desired = new Set(next.get(role.id) ?? role.permissionIds);
      if (checked) {
        desired.add(permission.id);
      } else {
        desired.delete(permission.id);
      }
      next.set(role.id, desired);
      return next;
    });
  }

  /** AC-806.12 — the dialog is the gate; nothing is sent from here. */
  save(): void {
    const changes = this.changes();
    if (!changes.length || this.saving()) {
      return;
    }

    this.confirmations
      .confirm({
        title: this.locale.t('permissions.saveConfirm.title', changes.length),
        message: this.locale.t('permissions.saveConfirm.body'),
        details: changes.map((change) => this.describe(change)),
        confirmText: this.locale.t('permissions.saveConfirm.confirm'),
        cancelText: this.locale.t('action.cancel'),
        danger: changes.some((change) => change.kind === 'revoke'),
      })
      .subscribe((accepted) => {
        if (accepted) {
          this.apply();
        }
      });
  }

  /** Task 10 (AC-806.18) puts a confirmation in front of this. */
  discard(): void {
    const model = this.data();
    if (!model || !this.isDirty()) {
      return;
    }
    this.draft.set(this.seedDraft(model, new Map()));
  }

  describe(change: PermissionChange): string {
    return this.locale.t(
      change.kind === 'grant' ? 'permissions.change.grant' : 'permissions.change.revoke',
      change.permissionName,
      change.roleName,
    );
  }

  /** Read by the route guard in Task 10 and by the `beforeunload` backstop below. */
  hasUnsavedChanges(): boolean {
    return this.isDirty();
  }

  onBeforeUnload(event: BeforeUnloadEvent): void {
    // The browser shows its own untranslatable prompt here; it is a backstop for a closed tab, not
    // a designed screen (spec A9). In-app navigation uses the styled dialog instead.
    if (this.hasUnsavedChanges()) {
      event.preventDefault();
    }
  }

  /**
   * One `PUT` per dirty role, sequentially. `concat` rather than `forkJoin`: the backend takes a
   * per-role lock, so parallelism buys nothing and would make a partial outcome depend on
   * interleaving.
   */
  private apply(): void {
    const model = this.data();
    if (!model) {
      return;
    }

    const draft = this.draft();
    const dirty = this.dirtyRoleIds();
    const roles = model.roles.filter((role) => dirty.includes(role.id));

    this.saving.set(true);

    const requests = roles.map((role) =>
      this.api
        .setRolePermissions(role.id, [...(draft.get(role.id) ?? [])], [...role.permissionIds])
        .pipe(
          map(() => ({ role, error: null as ApiError | null })),
          catchError((error: unknown) => of({ role, error: this.toApiError(error) })),
        ),
    );

    concat(...requests)
      .pipe(toArray())
      .subscribe((results) => {
        this.saving.set(false);
        const failures = results.filter((result) => result.error !== null);

        if (!failures.length) {
          this.toast.success(this.locale.t('permissions.saveSuccess'));
          this.load();
          return;
        }

        // Task 09 replaces this with the per-role banner, the stale-reload affordance and the
        // built-in-role message (AC-806.15…AC-806.17). Until then the failed roles' intent is
        // retained and the server's own message is surfaced — never swallowed.
        this.toast.error(
          this.locale.t('error.generic.title'),
          this.locale.t('permissions.mutationError'),
        );
        this.load(this.retainOf(failures.map((failure) => failure.role.id), draft));
      });
  }

  private seedDraft(model: PermissionAdministration, retain: Draft): Draft {
    const draft = new Map<string, ReadonlySet<string>>();
    for (const role of model.roles) {
      draft.set(role.id, new Set(retain.get(role.id) ?? role.permissionIds));
    }
    return draft;
  }

  private retainOf(roleIds: readonly string[], draft: Draft): Draft {
    const retained = new Map<string, ReadonlySet<string>>();
    for (const roleId of roleIds) {
      const desired = draft.get(roleId);
      if (desired) {
        retained.set(roleId, desired);
      }
    }
    return retained;
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError
      ? error
      : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
```

Note what is gone: `mutating`, `mutationSuccess` and `isAssigned`. `mutating` disabled the whole
matrix during a single-cell request, which no longer exists; `isAssigned` is replaced by
`isChecked`, which reads the draft rather than the snapshot. `mutationError` moves to Task 09's
structured banner.

- [ ] **Step 6: Update the template**

In `permissions.component.html`, replace the checkbox cell (`:50-61`) with:

```html
                      <td class="px-4 py-3">
                        <label class="inline-flex min-h-10 items-center gap-2">
                          <input
                            type="checkbox"
                            [checked]="isChecked(role.id, permission.id)"
                            [disabled]="saving()"
                            [attr.aria-label]="role.name + ': ' + permission.name"
                            [attr.aria-describedby]="isStaged(role.id, permission.id) ? 'permissions-staged-hint' : null"
                            (change)="toggle(role, permission, $any($event.target).checked)"
                            class="size-4 accent-primary"
                          />
                          @if (isStaged(role.id, permission.id)) {
                            <span
                              data-testid="staged-marker"
                              class="rounded bg-primary-container px-1.5 py-0.5 text-label-md font-semibold text-on-primary-container"
                            >
                              {{ 'permissions.staged' | t }}
                            </span>
                          }
                        </label>
                      </td>
```

Replace the success/error `sr-only` paragraphs (`:17-26`) with the staged-state hint the cells
reference:

```html
  <p id="permissions-staged-hint" class="sr-only">{{ 'permissions.stagedHint' | t }}</p>
```

And add the action bar as the last child of the root `<section>`, after the `</cs-card>`:

```html
  @if (isDirty()) {
    <div
      data-testid="permissions-action-bar"
      class="sticky bottom-4 z-40 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-border-subtle bg-surface-lowest px-4 py-3 shadow-popover"
    >
      <p class="text-label-lg text-on-surface" role="status" aria-live="polite">
        {{ 'permissions.pending' | t: changes().length }}
      </p>
      <div class="flex gap-2">
        <span data-testid="permissions-discard">
          <cs-button variant="secondary" type="button" [disabled]="saving()" (pressed)="discard()">
            {{ 'permissions.discard' | t }}
          </cs-button>
        </span>
        <span data-testid="permissions-save">
          <cs-button variant="primary" type="button" [busy]="saving()" (pressed)="save()">
            {{ 'permissions.save' | t: changes().length }}
          </cs-button>
        </span>
      </div>
    </div>
  }
```

The `data-testid` sits on a wrapping `<span>` because `cs-button` renders its own `<button>` and does
not forward arbitrary attributes (`button.component.ts:38-44`); the tests select
`[data-testid="permissions-save"] button`.

- [ ] **Step 7: Run the tests to verify they pass**

```bash
cd frontend && npx ng test admin-app --watch=false --include='**/permissions.component.spec.ts'
```

Expected: PASS. `AC805_4_CannotRemoveLastPermission` will now **fail** — it clicks a checkbox and
expects a `DELETE`. That test's criterion moves to Task 09; comment it out **only** if you commit
Task 09 immediately after, and say so in the commit message. Preferred: implement Task 09 before
committing either, and commit both together.

- [ ] **Step 8: Build**

```bash
cd frontend && npx ng build admin-app && npx ng test common --watch=false
```

Expected: `Build succeeded`; the `common` suite green (the api addition is additive).

- [ ] **Step 9: Commit**

```bash
git add frontend/projects/common/src/lib/admin/permission.api.ts \
        frontend/projects/common/src/lib/i18n/translations.ts \
        frontend/projects/admin-app/src/app/features/admin/permissions.component.ts \
        frontend/projects/admin-app/src/app/features/admin/permissions.component.html \
        frontend/projects/admin-app/src/app/features/admin/permissions.component.spec.ts
git commit -m "feat: stage permission edits and save them per role (AC-806.11..AC-806.14, AC-806.24)"
```

## Criteria covered

`AC-806.11`, `AC-806.12`, `AC-806.13`, `AC-806.14`, `AC-806.24`.

## Test evidence

*Not yet executed.*

## Deviations from the plan

*None yet.*
