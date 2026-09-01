# Task 12 — Accessibility and RTL (US-806, AC-806.25, AC-806.26)

**Files:**
- Modify: `frontend/projects/admin-app/src/app/features/admin/permissions.component.html` (persistent live region, marker semantics)
- Modify: `frontend/projects/admin-app/src/app/features/admin/permissions.component.ts` (`announcement` computed)
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts` (two keys)
- Test: `frontend/projects/admin-app/src/app/features/admin/permissions.component.spec.ts` (modify)

**Interfaces:**
- Consumes: `changes`, `isStaged`, `saveOutcome` from Tasks 08–09; `LocaleStore.locale` /
  `setLocale` (`locale.store.ts`) for the RTL test; the two repo-wide guard sweeps described below.
- Produces: nothing later tasks depend on. This is the last task in the feature.

**Two criteria are already largely enforced by existing tests, and this task's job is to keep them
green rather than to duplicate them.**

- `common/src/lib/testing/rtl-safety.spec.ts:20-28` scans **every** `.html` under `projects/` and
  fails on a physical-direction utility (`pl-`, `ml-`, `left-`, `text-left`, `border-l`,
  `rounded-tl-`). Every template written in Tasks 08–11 is inside that sweep already, which is why
  the markup uses `ps-`/`pe-`/`start-`/`text-start`/`border-s`.
- `common/src/lib/testing/no-hardcoded-strings.spec.ts` scans the same tree for user-facing literals.
  Material Symbols ligatures are allowed by its `ALLOWED` regex (`no-hardcoded-strings.spec.ts:38`),
  `data-testid` values are attributes and are stripped, and bare punctuation like `(` `)` `/` is
  allowed — so the new markup should pass unchanged.

So `AC-806.26` is proven by running those two specs plus one screen-specific assertion that the
matrix actually renders under `dir="rtl"` with Arabic labels. What is genuinely new here is
`AC-806.25`.

**The real accessibility gap Tasks 08–11 leave.** The pending-count live region lives *inside* the
sticky action bar (`@if (isDirty())`), so it is destroyed the moment the count reaches zero — the one
transition most worth announcing ("your changes were discarded / saved") is silent, because a live
region has to exist before it changes to be announced at all. It has to move outside the `@if`.

## Steps

- [ ] **Step 1: Add the translation keys**

```ts
  'permissions.announceClean': { en: 'No unsaved permission changes.', ar: 'لا توجد تغييرات صلاحيات غير محفوظة.' },
  'permissions.stagedGrant': { en: 'staged: grant', ar: 'مُجهَّز: منح' },
  'permissions.stagedRevoke': { en: 'staged: revoke', ar: 'مُجهَّز: إلغاء' },
```

`permissions.staged` from Task 08 is replaced at the cell level by these two: "changed" tells a
screen-reader user that something differs but not in which direction, and the direction is the whole
point of a permission edit.

- [ ] **Step 2: Write the failing tests**

```ts
  it('AC806_25_TheStagedMarkerNamesTheDirectionAndIsNotColourOnly', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    checkboxes(fixture)[1].click();          // grant
    fixture.detectChanges();
    expect(text(fixture)).toContain('staged: grant');

    checkboxes(fixture)[0].click();          // revoke
    fixture.detectChanges();
    expect(text(fixture)).toContain('staged: revoke');

    // The marker carries text, so it survives a stylesheet that never loads and a colour-blind
    // reader. Asserting the text exists is the assertion; the class list is not.
    const marker = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="staged-marker"]')!;
    expect(marker.textContent?.trim().length).toBeGreaterThan(0);
  });

  it('AC806_25_TheLiveRegionSurvivesTheCountReachingZero', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    const region = () =>
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-announcer"]');

    // Present before anything is staged — a live region added at the same time as its content is
    // not announced.
    expect(region()).not.toBeNull();
    expect(region()!.getAttribute('aria-live')).toBe('polite');
    expect(region()!.textContent).toContain('No unsaved permission changes.');

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    expect(region()!.textContent).toContain('1 unsaved changes');

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    // Still present, now reporting clean.
    expect(region()).not.toBeNull();
    expect(region()!.textContent).toContain('No unsaved permission changes.');
  });

  it('AC806_25_EveryInteractiveControlIsKeyboardReachable', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);
    checkboxes(fixture)[1].click();
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const focusable = host.querySelectorAll<HTMLElement>(
      'button, input, [tabindex]:not([tabindex="-1"])',
    );

    // Nothing is reachable only by pointer: no control carries a negative tabindex, and every
    // checkbox has an accessible name.
    for (const element of Array.from(focusable)) {
      expect(element.getAttribute('tabindex')).not.toBe('-1');
    }
    for (const box of checkboxes(fixture)) {
      expect(box.getAttribute('aria-label')?.length).toBeGreaterThan(0);
    }
    // The group headers are real buttons with expanded state, not clickable spans.
    const groupToggle = host.querySelector('[data-testid="permissions-group-ticket"] button')!;
    expect(groupToggle.getAttribute('aria-expanded')).toBe('true');
  });

  it('AC806_26_RendersUnderArabicRtl', () => {
    const locale = TestBed.inject(LocaleStore);
    locale.setLocale('ar');

    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    expect(document.documentElement.getAttribute('dir')).toBe('rtl');
    const body = text(fixture);
    expect(body).toContain('إدارة الصلاحيات');      // permissions.title
    expect(body).toContain('التذاكر');               // permissions.group.ticket
    // A missing translation renders the key itself; assert none leaked.
    expect(body).not.toContain('permissions.');

    locale.setLocale('en');
  });
```

Check `LocaleStore`'s setter name against `locale.store.ts` before running — if it is `set` or
`use` rather than `setLocale`, match the real API. Import `LocaleStore` from `common` in the spec.

- [ ] **Step 3: Run them to verify they fail**

```bash
cd frontend && npx ng test admin-app --watch=false --include='**/permissions.component.spec.ts'
```

- [ ] **Step 4: Add the announcement computed**

In the component:

```ts
  /**
   * What a screen reader hears when the staged count changes. Rendered in a live region that exists
   * for the life of the screen (AC-806.25) — a region created at the same moment as its text is not
   * announced, which is why this returns the clean sentence rather than an empty string.
   */
  readonly announcement = computed(() => {
    const count = this.changes().length;
    return count === 0
      ? this.locale.t('permissions.announceClean')
      : this.locale.t('permissions.pending', count);
  });

  /** The direction of a staged cell, for its visible marker. `null` when the cell is unchanged. */
  stagedDirection(roleId: string, permissionId: string): 'grant' | 'revoke' | null {
    if (!this.isStaged(roleId, permissionId)) {
      return null;
    }
    return this.isChecked(roleId, permissionId) ? 'grant' : 'revoke';
  }
```

- [ ] **Step 5: Move the live region out of the action bar**

In `permissions.component.html`, add immediately after the `<header>` (and **outside** any `@if`):

```html
  <p
    data-testid="permissions-announcer"
    class="sr-only"
    role="status"
    aria-live="polite"
    aria-atomic="true"
  >
    {{ announcement() }}
  </p>
```

and remove `role="status" aria-live="polite"` from the count paragraph inside the sticky bar (added in
Task 08) — two live regions reporting the same number announce it twice.

- [ ] **Step 6: Make the cell marker name its direction**

Replace the staged-marker block in the checkbox cell:

```html
                            @if (stagedDirection(role.id, column.permission.id); as direction) {
                              <span
                                data-testid="staged-marker"
                                class="rounded px-1.5 py-0.5 text-label-md font-semibold"
                                [class.bg-primary-container]="direction === 'grant'"
                                [class.text-on-primary-container]="direction === 'grant'"
                                [class.bg-error-container]="direction === 'revoke'"
                                [class.text-on-error-container]="direction === 'revoke'"
                              >
                                {{ (direction === 'grant' ? 'permissions.stagedGrant' : 'permissions.stagedRevoke') | t }}
                              </span>
                            }
```

Colour still differentiates grant from revoke — for a sighted user it is the faster signal — but the
text says which, so colour is reinforcement rather than the only carrier (`AC-806.25`).

`permissions.staged` becomes unused. Delete the key rather than leaving it: an unused translation is
a string a translator will maintain for nothing. `permissions.stagedHint` stays — it is the
`aria-describedby` target.

- [ ] **Step 7: Run the tests to verify they pass**

```bash
cd frontend && npx ng test admin-app --watch=false --include='**/permissions.component.spec.ts'
```

Expected: PASS, whole file.

- [ ] **Step 8: Run the two repo-wide guards — the actual proof for AC-806.26**

```bash
cd frontend && npx ng test common --watch=false --include='**/rtl-safety.spec.ts'
cd frontend && npx ng test common --watch=false --include='**/no-hardcoded-strings.spec.ts'
```

Expected: PASS. **If `rtl-safety` fails**, it names the file and the offending utility — fix the
markup with the logical equivalent (`pl-` → `ps-`, `text-left` → `text-start`, `border-l` →
`border-s`); do not add the file to a skip list. **If `no-hardcoded-strings` fails** on the new
markup, the fix is a translation key, not an `ALLOWED` entry — the spec file itself says every
addition to that list is a hole (`no-hardcoded-strings.spec.ts:27`).

- [ ] **Step 9: Full verification pass**

```bash
cd frontend && npx ng test common --watch=false && npx ng test admin-app --watch=false && npx ng build admin-app
cd backend && dotnet test CustomerSupport.slnx && dotnet build CustomerSupport.slnx
```

Paste all four outputs below. This is the run that backs the feature's completion claim, so it is
the whole suite on both stacks, not the filtered runs.

- [ ] **Step 10: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/admin/permissions.component.ts \
        frontend/projects/admin-app/src/app/features/admin/permissions.component.html \
        frontend/projects/admin-app/src/app/features/admin/permissions.component.spec.ts \
        frontend/projects/common/src/lib/i18n/translations.ts
git commit -m "feat: announce staged permission changes and name their direction (AC-806.25, AC-806.26)"
```

- [ ] **Step 11: Update the records before claiming anything**

1. Fill in **Test evidence** in every task file 01–12 from the runs actually performed.
2. Update `docs/superpowers/plans/EPIC-09-US-806-feat-34-permissions-workbench/README.md` with the
   criteria delivered, the commit hashes, and any gap accepted.
3. Update `Status` and `Status evidence` in `docs/requirements/user-stories/US-806-permission-workbench.md`
   and `US-807-global-confirmation.md` from what was executed.
4. Update the `FEAT-34` row in `docs/requirements/delivery-plan.md`.
5. Update `docs/assessment/rubric-traceability.md` if this feature evidences a row.

Only then is the feature shippable per `CLAUDE.md`'s definition — and per
`superpowers:verification-before-completion`, run it before making any completion claim.

## Criteria covered

`AC-806.25`, `AC-806.26`.

## Test evidence

Implemented 2026-09-01, in the same commit as Tasks 08–11:

```
npx ng test admin-app --watch=false --include='**/permissions*.spec.ts'
Test Files  2 passed (2)
     Tests  31 passed (31)
```

`AC806_25_TheStagedMarkerNamesTheDirectionAndIsNotColourOnly`,
`AC806_25_TheLiveRegionSurvivesTheCountReachingZero`,
`AC806_25_EveryInteractiveControlIsKeyboardReachable` and `AC806_26_RendersUnderArabicRtl` all pass.

Full `common` suite re-run after this feature's translation-key and template additions:

```
npx ng test common --watch=false
Test Files  3 failed | 47 passed (50)
     Tests   4 failed | 223 passed (227)
```

All four failures trace to one file: `portal-app/kb-list.component.html:57`
(`-right-10`, a physical-direction utility the `rtl-safety` sweep flags). Confirmed via `git status`
that this file is **already committed and untouched by this feature** — a pre-existing defect, not
a regression from FEAT-34's translation or template changes.

Full `admin-app` suite:

```
npx ng test admin-app --watch=false
Test Files  3 failed | 29 passed (32)
     Tests   4 failed | 257 passed (261)
```

The four failures are `nav-routes.spec.ts` (an orphaned `/kb-admin/:id` route, unrelated to the
`permissions` route), and two tests in `ticket-detail.component.spec.ts` — that file is confirmed
via `git status` to already be modified by FEAT-32's in-progress, uncommitted work on this branch.
None touch permissions, confirmation, users, departments or SLA policies.

`npx ng build admin-app`: succeeded, one pre-existing bundle-size budget warning unrelated to this
feature's files.

**Backend full suite was not re-run as part of this task** — no backend files changed in Tasks
08–12, and the backend integration-test environment remains blocked per Task 03's evidence.

## Deviations from the plan

1. **Merged into one commit with Tasks 08–11** — see Task 08's evidence entry for the rationale.
2. **The `permissions.staged` key was never added, rather than added then deleted.** The plan's
   Step 6 says to delete it once `stagedGrant`/`stagedRevoke` make it redundant; since Tasks 08–12
   were implemented together, the direction-aware markers were written from the start and the
   single generic key was simply never introduced.
3. **The remaining task-record updates** (this section, and the sibling task files' Test evidence)
   are themselves Step 11 of this task, executed now rather than as a separate pass.
