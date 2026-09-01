# Task 11 — Search, resource grouping and per-role bulk actions (US-806, AC-806.21…AC-806.23)

**Files:**
- Modify: `frontend/projects/admin-app/src/app/features/admin/permissions.component.ts` (columns, groups, search, bulk)
- Modify: `frontend/projects/admin-app/src/app/features/admin/permissions.component.html` (two-row header, summary cells, bulk cell, search field)
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts` (eleven new keys)
- Test: `frontend/projects/admin-app/src/app/features/admin/permissions.component.spec.ts` (modify)

**Interfaces:**
- Consumes: `draft`, `isChecked`, `toggle`, `changes` from Task 08; `TranslationKey` — exported from
  `common` via `public-api.ts:24` (`translations.ts:1528`), which is what makes the group-label map
  compile-time checked instead of a cast; `CsIcon`'s `name` input takes any Material Symbols
  ligature (`icon.component.ts:29`), so `expand_more` / `chevron_right` / `search` / `close` need no
  registration anywhere.
- Produces (Task 12 relies on these):
  - `type MatrixColumn = { kind: 'permission'; groupKey: string; permission: PermissionAdministrationPermission } | { kind: 'summary'; groupKey: string; count: number }`
  - `readonly search: WritableSignal<string>`, `readonly groups`, `readonly columns`, `readonly visiblePermissions`
  - `toggleGroup(key: string)`, `clearSearch()`, `grantAll(role)`, `revokeAll(role)`, `groupCount(roleId, groupKey)`

**Grouping comes from the data, not from new schema** (spec `A1`). `PermissionSeeder.cs:11-22` names
every permission `<resource>.<action>` — `ticket.create`, `customer.view`, `report.export`,
`user.manage` — so the group key is the substring before the first `.`, and a name without a `.`
falls into `other`. No migration, no group table, and a permission added to the catalogue later
groups itself.

**A collapsed group keeps one column, not zero.** Two header rows have to stay aligned: row 1 spans
each group, row 2 lists its columns. If a collapsed group contributed no row-2 cell, every column to
its right would shift. So a collapsed group renders exactly one **summary** column showing that
role's `assigned / total` for the group — which is more useful than a blank anyway, and is where
`AC-806.22`'s per-group count lives.

## Steps

- [ ] **Step 1: Add the translation keys**

```ts
  'permissions.searchPlaceholder': { en: 'Search permissions', ar: 'البحث في الصلاحيات' },
  'permissions.clearSearch': { en: 'Clear search', ar: 'مسح البحث' },
  'permissions.noMatch': { en: 'No permission matches this search.', ar: 'لا توجد صلاحية مطابقة لهذا البحث.' },
  'permissions.grantAll': { en: 'Grant all', ar: 'منح الكل' },
  'permissions.revokeAll': { en: 'Revoke all', ar: 'إلغاء الكل' },
  'permissions.bulk': { en: 'Bulk', ar: 'إجراء جماعي' },
  'permissions.collapseGroup': { en: 'Collapse {0}', ar: 'طي {0}' },
  'permissions.expandGroup': { en: 'Expand {0}', ar: 'توسيع {0}' },
  'permissions.group.ticket': { en: 'Tickets', ar: 'التذاكر' },
  'permissions.group.customer': { en: 'Customers', ar: 'العملاء' },
  'permissions.group.report': { en: 'Reports', ar: 'التقارير' },
  'permissions.group.user': { en: 'Users', ar: 'المستخدمون' },
  'permissions.group.other': { en: 'Other', ar: 'أخرى' },
```

- [ ] **Step 2: Write the failing tests**

Append to `permissions.component.spec.ts`. `MODEL_TWO_ROLES` already spans two groups
(`ticket.view`, `ticket.close`, `report.view`), which is what these need:

```ts
  function searchInput(fixture: ComponentFixture<PermissionsComponent>): HTMLInputElement {
    return (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('[data-testid="permissions-search"]')!;
  }

  function type(fixture: ComponentFixture<PermissionsComponent>, value: string): void {
    const input = searchInput(fixture);
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('AC806_21_SearchNarrowsTheColumns', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);
    expect(checkboxes(fixture).length).toBe(6);   // 2 roles × 3 permissions

    type(fixture, 'report');

    expect(checkboxes(fixture).length).toBe(2);   // 2 roles × 1 permission
    expect(text(fixture)).toContain('report.view');
    expect(text(fixture)).not.toContain('ticket.close');
  });

  it('AC806_21_SearchMatchesTheDescriptionToo', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    type(fixture, 'Close tickets');

    expect(checkboxes(fixture).length).toBe(2);
    expect(text(fixture)).toContain('ticket.close');
  });

  it('AC806_21_NoMatchShowsAnInTableMessageNotTheEmptyState', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    type(fixture, 'zzzz');

    const body = text(fixture);
    expect(body).toContain('No permission matches this search.');
    // The page-level empty state means "this role/permission catalogue is empty" and would be a lie.
    expect(body).not.toContain('No permissions found.');
    expect(checkboxes(fixture).length).toBe(0);

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-clear-search"]')!
      .click();
    fixture.detectChanges();

    expect(checkboxes(fixture).length).toBe(6);
  });

  it('AC806_21_SearchDoesNotDiscardStagedChanges', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    checkboxes(fixture)[1].click();               // stage a ticket.* change
    fixture.detectChanges();
    type(fixture, 'report');                      // filter it out of view
    expect(text(fixture)).toContain('1 unsaved changes');

    type(fixture, '');                            // and back
    expect(checkboxes(fixture)[1].checked).toBe(true);
  });

  it('AC806_22_GroupsRenderWithCountsAndCollapse', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    const groupHeaders = (fixture.nativeElement as HTMLElement)
      .querySelectorAll('[data-testid^="permissions-group-"]');
    expect(groupHeaders.length).toBe(2);          // ticket, report
    expect(text(fixture)).toContain('Tickets');
    expect(text(fixture)).toContain('Reports');

    // Collapsing hides the group's columns and leaves one summary column per role instead.
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-group-ticket"] button')!
      .click();
    fixture.detectChanges();

    expect(checkboxes(fixture).length).toBe(2);   // only report.view remains interactive
    const summaries = (fixture.nativeElement as HTMLElement)
      .querySelectorAll('[data-testid="permissions-group-summary"]');
    expect(summaries.length).toBe(2);             // one per role
    expect(summaries[0].textContent).toContain('1/2');   // Admin holds ticket.view of two ticket.*
  });

  it('AC806_22_CollapsingAGroupKeepsItsStagedChanges', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    expect(text(fixture)).toContain('1 unsaved changes');

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-group-ticket"] button')!
      .click();
    fixture.detectChanges();

    expect(text(fixture)).toContain('1 unsaved changes');
    // And the collapsed summary reflects the draft, not the snapshot.
    expect(
      (fixture.nativeElement as HTMLElement)
        .querySelectorAll('[data-testid="permissions-group-summary"]')[0].textContent,
    ).toContain('2/2');
  });

  it('AC806_23_GrantAllStagesEveryVisiblePermissionWithoutSending', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-grant-all-role-1"] button')!
      .click();
    fixture.detectChanges();

    http.expectNone(() => true);
    // Admin held 1 of 3, so 2 cells changed — the count reflects real changes, not clicks.
    expect(text(fixture)).toContain('2 unsaved changes');
    expect(checkboxes(fixture).slice(0, 3).every((box) => box.checked)).toBe(true);
  });

  it('AC806_23_BulkActionsRespectTheSearchFilter', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    type(fixture, 'report');
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-grant-all-role-1"] button')!
      .click();
    fixture.detectChanges();

    // Only the visible column was staged.
    expect(text(fixture)).toContain('1 unsaved changes');

    type(fixture, '');
    expect(checkboxes(fixture)[1].checked).toBe(false, 'ticket.close was filtered out and untouched');
  });

  it('AC806_23_RevokeAllStagesRemovalOfVisiblePermissions', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-revoke-all-role-1"] button')!
      .click();
    fixture.detectChanges();

    http.expectNone(() => true);
    expect(text(fixture)).toContain('1 unsaved changes');   // Admin held exactly one
    expect(checkboxes(fixture).slice(0, 3).some((box) => box.checked)).toBe(false);
  });
```

- [ ] **Step 3: Run them to verify they fail**

```bash
cd frontend && npx ng test admin-app --watch=false --include='**/permissions.component.spec.ts'
```

- [ ] **Step 4: Add the grouping model to the component**

Above the class:

```ts
/** A permission column, or the single placeholder column a collapsed group leaves behind. */
export type MatrixColumn =
  | { readonly kind: 'permission'; readonly groupKey: string; readonly permission: PermissionAdministrationPermission }
  | { readonly kind: 'summary'; readonly groupKey: string; readonly count: number };

interface PermissionGroup {
  readonly key: string;
  readonly label: string;
  readonly permissions: readonly PermissionAdministrationPermission[];
  readonly collapsed: boolean;
}

/**
 * Group keys come from the permission names themselves (spec A1), so this map only supplies the
 * human label. `TranslationKey` keeps it honest: a typo is a compile error, and a group with no
 * entry falls back to its raw key rather than rendering a blank header.
 */
const GROUP_LABELS: Readonly<Record<string, TranslationKey>> = {
  ticket: 'permissions.group.ticket',
  customer: 'permissions.group.customer',
  report: 'permissions.group.report',
  user: 'permissions.group.user',
  other: 'permissions.group.other',
};
```

In the class (add `TranslationKey` to the `common` import):

```ts
  readonly search = signal('');
  readonly collapsedGroups = signal<ReadonlySet<string>>(new Set());

  readonly groups = computed<readonly PermissionGroup[]>(() => {
    const model = this.data();
    if (!model) {
      return [];
    }

    const term = this.search().trim().toLowerCase();
    const collapsed = this.collapsedGroups();
    const buckets = new Map<string, PermissionAdministrationPermission[]>();

    for (const permission of model.permissions) {
      const haystack = `${permission.name} ${permission.description ?? ''}`.toLowerCase();
      if (term && !haystack.includes(term)) {
        continue;
      }

      const separator = permission.name.indexOf('.');
      const key = separator > 0 ? permission.name.slice(0, separator) : 'other';
      const bucket = buckets.get(key);
      if (bucket) {
        bucket.push(permission);
      } else {
        buckets.set(key, [permission]);
      }
    }

    return [...buckets].map(([key, permissions]) => ({
      key,
      label: GROUP_LABELS[key] ? this.locale.t(GROUP_LABELS[key]) : key,
      permissions,
      collapsed: collapsed.has(key),
    }));
  });

  /** Header row 2 and every body row iterate this, so the two stay aligned by construction. */
  readonly columns = computed<readonly MatrixColumn[]>(() =>
    this.groups().flatMap((group) =>
      group.collapsed
        ? [{ kind: 'summary' as const, groupKey: group.key, count: group.permissions.length }]
        : group.permissions.map((permission) => ({
            kind: 'permission' as const,
            groupKey: group.key,
            permission,
          })),
    ),
  );

  /** The permissions a bulk action or a save-visible-count applies to (AC-806.23). */
  readonly visiblePermissions = computed<readonly PermissionAdministrationPermission[]>(() =>
    this.groups().filter((group) => !group.collapsed).flatMap((group) => group.permissions),
  );

  readonly hasNoMatch = computed(() => this.search().trim().length > 0 && this.groups().length === 0);

  toggleGroup(key: string): void {
    this.collapsedGroups.update((collapsed) => {
      const next = new Set(collapsed);
      if (!next.delete(key)) {
        next.add(key);
      }
      return next;
    });
  }

  clearSearch(): void {
    this.search.set('');
  }

  /** `assigned / total` for one role within one group, read from the draft (AC-806.22). */
  groupCount(roleId: string, groupKey: string): string {
    const group = this.groups().find((candidate) => candidate.key === groupKey);
    if (!group) {
      return '';
    }
    const assigned = group.permissions.filter((permission) => this.isChecked(roleId, permission.id)).length;
    return `${assigned}/${group.permissions.length}`;
  }

  /** AC-806.23 — stages every *visible* permission for this role. Nothing is sent. */
  grantAll(role: PermissionAdministrationRole): void {
    this.stageBulk(role, true);
  }

  revokeAll(role: PermissionAdministrationRole): void {
    this.stageBulk(role, false);
  }

  private stageBulk(role: PermissionAdministrationRole, granted: boolean): void {
    if (this.saving()) {
      return;
    }

    const visible = this.visiblePermissions();
    this.draft.update((draft) => {
      const next = new Map(draft);
      const desired = new Set(next.get(role.id) ?? role.permissionIds);
      for (const permission of visible) {
        if (granted) {
          desired.add(permission.id);
        } else {
          desired.delete(permission.id);
        }
      }
      next.set(role.id, desired);
      return next;
    });
  }
```

- [ ] **Step 5: Rewrite the table in the template**

Replace the `<thead>`/`<tbody>` (`permissions.component.html:37-65`) with:

```html
              <thead>
                <tr class="border-b border-border-subtle bg-surface-low text-label-md text-on-surface-variant">
                  <th scope="col" rowspan="2" class="px-4 py-3 text-start align-bottom">{{ 'permissions.role' | t }}</th>
                  @for (group of groups(); track group.key) {
                    <th
                      scope="colgroup"
                      [attr.data-testid]="'permissions-group-' + group.key"
                      [attr.colspan]="group.collapsed ? 1 : group.permissions.length"
                      class="border-s border-border-subtle px-4 py-2 text-start"
                    >
                      <button
                        type="button"
                        class="inline-flex items-center gap-1 rounded text-label-md font-semibold text-on-surface hover:underline"
                        [attr.aria-expanded]="!group.collapsed"
                        [attr.aria-label]="(group.collapsed ? 'permissions.expandGroup' : 'permissions.collapseGroup') | t: group.label"
                        (click)="toggleGroup(group.key)"
                      >
                        <cs-icon [name]="group.collapsed ? 'chevron_right' : 'expand_more'" [size]="16" />
                        {{ group.label }} ({{ group.permissions.length }})
                      </button>
                    </th>
                  }
                  <th scope="col" rowspan="2" class="border-s border-border-subtle px-4 py-3 text-end align-bottom">
                    {{ 'permissions.bulk' | t }}
                  </th>
                </tr>
                <tr class="border-b border-border-subtle bg-surface-low text-label-md text-on-surface-variant">
                  @for (column of columns(); track column.kind + ':' + (column.kind === 'permission' ? column.permission.id : column.groupKey)) {
                    <th scope="col" class="px-4 py-3 text-start font-normal">
                      @if (column.kind === 'permission') {
                        <span [title]="column.permission.description ?? undefined">{{ column.permission.name }}</span>
                      } @else {
                        <span class="text-on-surface-variant">{{ 'permissions.assigned' | t }}</span>
                      }
                    </th>
                  }
                </tr>
              </thead>
              <tbody>
                @for (role of model.roles; track role.id) {
                  <tr class="border-b border-border-subtle last:border-transparent">
                    <th scope="row" class="px-4 py-3 text-start text-label-lg text-on-surface">{{ role.name }}</th>
                    @for (column of columns(); track column.kind + ':' + (column.kind === 'permission' ? column.permission.id : column.groupKey)) {
                      <td class="px-4 py-3">
                        @if (column.kind === 'permission') {
                          <label class="inline-flex min-h-10 items-center gap-2">
                            <input
                              type="checkbox"
                              [checked]="isChecked(role.id, column.permission.id)"
                              [disabled]="saving()"
                              [attr.aria-label]="role.name + ': ' + column.permission.name"
                              [attr.aria-describedby]="isStaged(role.id, column.permission.id) ? 'permissions-staged-hint' : null"
                              (change)="toggle(role, column.permission, $any($event.target).checked)"
                              class="size-4 accent-primary"
                            />
                            @if (isStaged(role.id, column.permission.id)) {
                              <span
                                data-testid="staged-marker"
                                class="rounded bg-primary-container px-1.5 py-0.5 text-label-md font-semibold text-on-primary-container"
                              >
                                {{ 'permissions.staged' | t }}
                              </span>
                            }
                          </label>
                        } @else {
                          <span
                            data-testid="permissions-group-summary"
                            class="font-data-mono text-body-sm text-on-surface-variant"
                          >
                            {{ groupCount(role.id, column.groupKey) }}
                          </span>
                        }
                      </td>
                    }
                    <td class="px-4 py-3">
                      <div class="flex justify-end gap-1">
                        <span [attr.data-testid]="'permissions-grant-all-' + role.id">
                          <cs-button variant="ghost" type="button" [disabled]="saving()" (pressed)="grantAll(role)">
                            {{ 'permissions.grantAll' | t }}
                          </cs-button>
                        </span>
                        <span [attr.data-testid]="'permissions-revoke-all-' + role.id">
                          <cs-button variant="ghost" type="button" [disabled]="saving()" (pressed)="revokeAll(role)">
                            {{ 'permissions.revokeAll' | t }}
                          </cs-button>
                        </span>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
```

Add the search row directly above the `<div class="overflow-x-auto">`:

```html
          <div class="mb-4 flex flex-wrap items-center gap-2">
            <label class="relative flex min-w-64 items-center">
              <span class="sr-only">{{ 'permissions.searchPlaceholder' | t }}</span>
              <cs-icon name="search" [size]="18" class="absolute start-3 text-on-surface-variant" />
              <input
                data-testid="permissions-search"
                type="search"
                [value]="search()"
                [placeholder]="'permissions.searchPlaceholder' | t"
                (input)="search.set($any($event.target).value)"
                class="w-full rounded-lg border border-outline-variant bg-surface-lowest py-2 pe-3 ps-10 text-body-md text-on-surface"
              />
            </label>
            @if (search()) {
              <span data-testid="permissions-clear-search">
                <cs-button variant="ghost" type="button" (pressed)="clearSearch()">
                  <cs-icon name="close" [size]="16" />
                  {{ 'permissions.clearSearch' | t }}
                </cs-button>
              </span>
            }
          </div>

          @if (hasNoMatch()) {
            <p data-testid="permissions-no-match" class="px-4 py-6 text-body-md text-on-surface-variant">
              {{ 'permissions.noMatch' | t }}
            </p>
          }
```

and wrap the existing `<div class="overflow-x-auto">` in `@if (!hasNoMatch()) { … }` so the table is
replaced by the message rather than rendering an empty grid.

`ps-*`/`pe-*`/`text-start`/`border-s` are the logical-direction utilities the codebase already uses
(`permissions.component.html:39`) — they flip under RTL, which is what Task 12 verifies.

- [ ] **Step 6: Run the tests to verify they pass**

```bash
cd frontend && npx ng test admin-app --watch=false --include='**/permissions.component.spec.ts'
```

Expected: PASS, whole file. Two likely first failures and their real causes:

- **`AC806_22_GroupsRenderWithCountsAndCollapse` sees the wrong summary text** — `groupCount` reads
  the *filtered* group, so a search term changes the denominator. That is intended; if the test
  disagrees, fix the test's expectation, not the denominator.
- **`@for` track expression rejected** — a `track` cannot always reference a discriminated union
  property directly. If the compiler complains, add a `columnKey(column: MatrixColumn): string`
  method and track by that instead of inlining the ternary.

- [ ] **Step 7: Build**

```bash
cd frontend && npx ng build admin-app
```

- [ ] **Step 8: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/admin/permissions.component.ts \
        frontend/projects/admin-app/src/app/features/admin/permissions.component.html \
        frontend/projects/admin-app/src/app/features/admin/permissions.component.spec.ts \
        frontend/projects/common/src/lib/i18n/translations.ts
git commit -m "feat: search, resource grouping and per-role bulk staging in the permission matrix (AC-806.21..AC-806.23)"
```

## Criteria covered

`AC-806.21`, `AC-806.22`, `AC-806.23`.

## Test evidence

Implemented 2026-09-01, in the same commit as Tasks 08, 09, 10 and 12:

```
npx ng test admin-app --watch=false --include='**/permissions*.spec.ts'
Test Files  2 passed (2)
     Tests  31 passed (31)
```

All nine `AC806_21_*`/`AC806_22_*`/`AC806_23_*` tests pass, including the search/collapse/bulk
interaction tests and the "respects the search filter" bulk-action test.

## Deviations from the plan

1. **Merged into one commit with Tasks 08, 09, 10, 12** — see Task 08's evidence entry.
2. **`columns` needed an explicit return-type annotation on `flatMap`.** The plan's ternary
   (`group.collapsed ? [...] : group.permissions.map(...)`) produced a TypeScript union-narrowing
   error (TS2322): each branch's array-element type was inferred independently and the two did not
   unify into `MatrixColumn[]`. Fixed by annotating the `flatMap` callback as
   `(group): MatrixColumn[] => …` and dropping the now-redundant `as const` tags on `kind`, letting
   the annotation carry the literal-type information instead.
3. **Track expression extracted to a method.** The plan's inline `track column.kind + ':' +
   (column.kind === 'permission' ? column.permission.id : column.groupKey)` was replaced with a
   `columnKey(column: MatrixColumn): string` method on the component, used identically in the header
   and body rows — an inline ternary inside a `@for` track expression is harder to keep in sync
   across the two loops that must track identically for `AC-806.22`'s alignment guarantee to hold.
