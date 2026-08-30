# US-310 Branch Administration UI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** An Admin-only Angular screen (list/create/edit/deactivate) for `Branch`, mirroring
`DepartmentsComponent`'s shape exactly — the backend contract (`BranchesController`) has existed
since `FEAT-16` with no frontend consumer at all.

**Architecture:** One new frontend API client (`BranchApi`, matching `DepartmentApi`'s shape
verbatim) and one new admin screen (`BranchesComponent`), following `DepartmentsComponent`'s
pattern file-for-file — read `frontend/projects/admin-app/src/app/features/organisation/
departments.component.ts`/`.html` before writing a line here; the code below is that file with
`Department`→`Branch` and two extra fields (`region`, `timezone`).

**Tech Stack:** Angular 20 standalone components, signals, `AsyncState`, reactive forms.

**Spec:** `docs/superpowers/specs/EPIC-13-EPIC-13-US-310-branch-admin-ui.md` (and the mini-spec section below).

**Not implemented this pass.** This plan is written and committed ahead of any code that implements
it, per explicit instruction — execution is a future session's work.

---

## Mini-Spec (OQ-5 gate, `AC-310.1`)

`OQ-5` (branch-scoped visibility) is still open at the product level — `US-306`'s plan hits the same
blocker from the query side. `AC-310.1` therefore gates all UI work behind an explicit, narrow
contract decision recorded here, not silently resolved. The reading below is the plan's stated
assumption and must be signed off before Task 2 starts.

**Scope this screen includes (approved contract):**

- **Actor & server permission:** Admin only for create/update/deactivate. The route guard
  (`roleGuard('Admin')`) is a courtesy; `BranchesController`'s own `[Authorize(Policy = "Admin")]` on
  every mutation is the actual control — matching `DepartmentsComponent`'s own documented reasoning in
  `app.routes.ts`.
- **Route & shell placement:** `/branches`, lazy-loaded under the admin shell, `NAV_ITEMS` entry
  `{ path: '/branches', key: 'nav.branches', icon: 'location_on', adminOnly: true }` after
  `departments`. No branch-scoped visibility, branch membership, or `OQ-5` behaviour anywhere.
- **List response & columns:** `id`, `name`, `region`, `timezone`, `isActive`, `createdAt` — paged
  `PagedResult<Branch>` from `GET /api/Branches`.
- **Request contract:** `POST`/`PUT /api/Branches` payload `{ name, region?, timezone? }`.
- **Validation:** `name` required, trimmed, ≤ 200 chars; `timezone` required; keyed envelope field
  errors mapped onto the matching input via `ApiError.fieldError(field)`.
- **Lifecycle:** active/inactive pill; deactivate is a soft `DELETE` returning 200 (not 204); inactive
  rows stay in the list; repeated deactivate is a no-op on an already-inactive row.
- **States:** loading (`cs-loading-state`), empty (`cs-empty-state`), error/retry (`cs-error-state`),
  and a non-field form error block; 401/403 are handled by the auth/role guard, 404 only on a stale
  deactivate target.
- **Responsive / RTL / a11y:** inherit `US-311`/`US-312` global behaviour — table scrolls inside a
  bounded `overflow-x-auto` region, logical properties only, focus order preserved.

**Scope this screen explicitly excludes (the OQ-5 surface):** branch-scoped ticket/customer queries,
branch membership assignment, "which branch am I" switching, and any per-branch data filtering. Those
are `US-306`'s problem; recording them here would be deciding `OQ-5` by UI fiat, which is the defect
`AC-310.1` exists to prevent.

**Contract verified, not invented:** `backend/src/CustomerSupport.InternalApi/Controllers/
BranchesController.cs` exposes `GET /api/Branches`, `GET /api/Branches/{id}`, `POST /api/Branches`,
`PUT /api/Branches/{id}`, `DELETE /api/Branches/{id}`. `BranchRequest` lives in
`backend/src/CustomerSupport.Application/Features/Organisation/Dtos/BranchDtos.cs` and the API uses the
standard `Response<T>` envelope + `PaginatedList<T>`. The UI consumes exactly that; it does not
compensate for any mismatch.

---

## Global Constraints

- No client-side branch scope is introduced anywhere — confirmed against the real `BranchesController`,
  which exposes only global list/create/update/deactivate with no scoping parameter.
- Every API call goes through the envelope interceptor (failures surface as `ApiError`, never as a
  silent empty list) —same rule `DepartmentsComponent` follows, same reason `async-state.ts` states.

---

### Task 1: `BranchApi` (`AC-310.1`, `AC-310.2`)

**Files:**
- Create: `frontend/projects/common/src/lib/organisation/branch.api.ts`
- Create: `frontend/projects/common/src/lib/organisation/branch.api.spec.ts`
- Modify: `frontend/projects/common/src/public-api.ts`

**Interfaces:**
- Consumes real, already-shipped backend shapes (confirmed against `BranchesController.cs` and its
  handlers): `BranchDto(Guid Id, string Name, string? Region, string Timezone, bool IsActive,
  DateTime CreatedAt)`, `BranchRequest(string Name, string? Region, string? Timezone)`. Routes:
  `GET /api/Branches` (paged), `GET /api/Branches/{id}`, `POST /api/Branches` (Admin), `PUT
  /api/Branches/{id}` (Admin), `DELETE /api/Branches/{id}` (Admin, soft-deactivate, 200 not 204).

- [ ] **Step 1: Write the failing test**

```ts
// frontend/projects/common/src/lib/organisation/branch.api.spec.ts
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { envelopeInterceptor } from 'common';
import { BranchApi } from './branch.api';

function ok<T>(data: T) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

describe('BranchApi', () => {
  let api: BranchApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    api = TestBed.inject(BranchApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists branches from /api/Branches', () => {
    let result: unknown;
    api.list().subscribe((r) => (result = r));

    http
      .expectOne((r) => r.url === '/api/Branches')
      .flush(ok({ items: [], pageIndex: 1, pageSize: 100, totalCount: 0 }));

    expect(result).toEqual({ items: [], pageIndex: 1, pageSize: 100, totalCount: 0 });
  });

  it('creates a branch via POST /api/Branches with name/region/timezone', () => {
    api.create({ name: 'North Region', region: 'North', timezone: 'UTC' }).subscribe();

    const request = http.expectOne('/api/Branches');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'North Region', region: 'North', timezone: 'UTC' });
    request.flush(ok({ id: 'b-1' }));
  });

  it('updates a branch via PUT /api/Branches/{id}', () => {
    api.update('b-1', { name: 'North', region: 'North', timezone: 'Europe/London' }).subscribe();

    const request = http.expectOne('/api/Branches/b-1');
    expect(request.request.method).toBe('PUT');
    request.flush(ok(null));
  });

  it('soft-deactivates via DELETE /api/Branches/{id}', () => {
    api.deactivate('b-1').subscribe();

    const request = http.expectOne('/api/Branches/b-1');
    expect(request.request.method).toBe('DELETE');
    request.flush(ok(null));
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test common --watch=false --include='**/branch.api.spec.ts'`
Expected: FAIL — `BranchApi` doesn't exist.

- [ ] **Step 3: Implement, matching `DepartmentApi` exactly**

```ts
// frontend/projects/common/src/lib/organisation/branch.api.ts
import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../api/api-response';

/** Matches the backend's `BranchDto` — FEAT-16, AC-116/AC-117. */
export interface Branch {
  readonly id: string;
  readonly name: string;
  readonly region: string | null;
  readonly timezone: string;
  readonly isActive: boolean;
  readonly createdAt: string;
}

/** The create/update payload — AC-120. */
export interface BranchRequest {
  readonly name: string;
  readonly region?: string | null;
  readonly timezone?: string | null;
}

/**
 * Branch administration calls. Catches nothing: failures arrive as `ApiError` from the envelope
 * interceptor, matching every other API service in this workspace.
 */
@Injectable({ providedIn: 'root' })
export class BranchApi {
  private readonly http = inject(HttpClient);

  list(): Observable<PagedResult<Branch>> {
    return this.http.get<PagedResult<Branch>>('/api/Branches', { params: { pageSize: '100' } });
  }

  create(request: BranchRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/Branches', request);
  }

  update(id: string, request: BranchRequest): Observable<unknown> {
    return this.http.put(`/api/Branches/${id}`, request);
  }

  /** Soft-deactivates — AC-120. */
  deactivate(id: string): Observable<unknown> {
    return this.http.delete(`/api/Branches/${id}`);
  }
}
```

In `public-api.ts`, add `export * from './lib/organisation/branch.api';` — it sits beside the existing
`export * from './lib/organisation/organisation.api';` and `sla-policy.api` lines (line 41–42).

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx ng test common --watch=false --include='**/branch.api.spec.ts'`
Expected: PASS, 4/4.

- [ ] **Step 5: Commit**

```bash
git add frontend/projects/common/src/lib/organisation/branch.api.ts frontend/projects/common/src/lib/organisation/branch.api.spec.ts frontend/projects/common/src/public-api.ts
git commit -m "feat(branches): BranchApi client (US-310 T1)"
```

---

### Task 2: `BranchesComponent` (`AC-310.2`)

**Files:**
- Create: `frontend/projects/admin-app/src/app/features/organisation/branches.component.ts`
- Create: `frontend/projects/admin-app/src/app/features/organisation/branches.component.html`
- Create: `frontend/projects/admin-app/src/app/features/organisation/branches.component.spec.ts`
- Modify: `frontend/projects/admin-app/src/app/app.routes.ts`
- Modify: `frontend/projects/admin-app/src/app/layout/shell.component.ts`
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts`

**Interfaces:**
- Consumes: `BranchApi` (Task 1).

- [ ] **Step 1: Write the failing test**

```ts
// frontend/projects/admin-app/src/app/features/organisation/branches.component.spec.ts
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { envelopeInterceptor } from 'common';
import BranchesComponent from './branches.component';

function ok<T>(data: T) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

const BRANCH = {
  id: 'b-1',
  name: 'North Region',
  region: 'North',
  timezone: 'UTC',
  isActive: true,
  createdAt: '2026-08-27T00:00:00Z',
};

describe('BranchesComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function render(): ComponentFixture<BranchesComponent> {
    const fixture = TestBed.createComponent(BranchesComponent);
    fixture.detectChanges();
    http
      .expectOne((r) => r.url === '/api/Branches')
      .flush(ok({ items: [BRANCH], pageIndex: 1, pageSize: 100, totalCount: 1 }));
    fixture.detectChanges();
    return fixture;
  }

  it('AC310.2: renders branches returned by the api', () => {
    const fixture = render();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('North Region');
  });

  it('AC310.2: shows the non-field form error from a server rejection', () => {
    const fixture = render();

    fixture.componentInstance.showCreate.set(true);
    fixture.componentInstance.form.setValue({ name: 'South Region', region: 'South', timezone: 'UTC' });
    fixture.componentInstance.create();

    const request = http.expectOne((r) => r.url === '/api/Branches' && r.method === 'POST');
    request.flush({
      success: false,
      code: 'BRANCH_NAME_EXISTS',
      message: 'A branch with this name already exists',
      data: null,
      errors: [{ field: 'Name', code: 'ERR_DUP', message: 'A branch with this name already exists' }],
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.fieldError('name')?.message).toContain('already exists');
  });

  it('AC310.2: create dialog posts the form and refreshes the list', () => {
    const fixture = render();

    fixture.componentInstance.showCreate.set(true);
    fixture.componentInstance.form.setValue({ name: 'South Region', region: 'South', timezone: 'UTC' });
    fixture.componentInstance.create();

    const request = http.expectOne((r) => r.url === '/api/Branches' && r.method === 'POST');
    expect(request.request.body).toEqual({ name: 'South Region', region: 'South', timezone: 'UTC' });
    request.flush(ok({ id: 'b-2' }));

    http
      .expectOne((r) => r.url === '/api/Branches')
      .flush(ok({ items: [BRANCH], pageIndex: 1, pageSize: 100, totalCount: 1 }));
    expect(fixture.componentInstance.showCreate()).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/branches.component.spec.ts'`
Expected: FAIL — module doesn't exist.

- [ ] **Step 3: Implement, mirroring `DepartmentsComponent` with the added `region`/`timezone` fields**

```ts
// frontend/projects/admin-app/src/app/features/organisation/branches.component.ts
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ApiError,
  AsyncState,
  Branch,
  BranchApi,
  CsButton,
  CsCard,
  CsDialog,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsInputField,
  CsLoadingState,
  failed,
  fromList,
  loading,
  LocaleStore,
  TranslatePipe,
} from 'common';

/**
 * US-310 — branch administration: list, create (dialog), and deactivation. Mirrors
 * `DepartmentsComponent`'s shape exactly, since `BranchesController`'s contract is the same
 * lookup-entity CRUD shape `Department` already established (`FEAT-16`). No branch-scoped
 * visibility here — `OQ-5` (mini-spec gate) explicitly stays out of scope.
 */
@Component({
  selector: 'admin-branches',
  imports: [
    CsCard,
    CsDialog,
    CsIcon,
    ReactiveFormsModule,
    CsInputField,
    CsButton,
    CsLoadingState,
    CsEmptyState,
    CsErrorState,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './branches.component.html',
})
export default class BranchesComponent {
  private readonly api = inject(BranchApi);

  protected readonly locale = inject(LocaleStore);

  readonly state = signal<AsyncState<readonly Branch[]>>(loading());
  readonly saving = signal(false);
  readonly createError = signal<ApiError | null>(null);
  readonly showCreate = signal(false);

  readonly items = computed<readonly Branch[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : [];
  });

  readonly listError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    region: new FormControl('', { nonNullable: true }),
    timezone: new FormControl('UTC', { nonNullable: true, validators: [Validators.required] }),
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set(loading());
    this.api.list().subscribe({
      next: (result) => this.state.set(fromList(result.items)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  create(): void {
    if (this.form.invalid || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.createError.set(null);

    this.api.create(this.form.getRawValue()).subscribe({
      next: () => {
        this.saving.set(false);
        this.form.reset({ name: '', region: '', timezone: 'UTC' });
        this.showCreate.set(false);
        this.load();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.createError.set(this.toApiError(error));
      },
    });
  }

  deactivate(branch: Branch): void {
    this.api.deactivate(branch.id).subscribe({
      next: () => this.load(),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  fieldError(field: string) {
    return this.createError()?.fieldError(field) ?? null;
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError
      ? error
      : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
```

```html
<!-- frontend/projects/admin-app/src/app/features/organisation/branches.component.html -->
<section class="flex flex-col gap-6">
  <header class="flex flex-wrap items-center justify-between gap-4">
    <div>
      <h1 class="font-display text-headline-lg text-on-surface">{{ 'branches.title' | t }}</h1>
      <p class="mt-1 text-body-md text-on-surface-variant">{{ 'branches.subtitle' | t }}</p>
    </div>
    <cs-button (pressed)="showCreate.set(true)">
      <cs-icon name="add" />
      {{ 'branches.add' | t }}
    </cs-button>
  </header>

  <cs-dialog
    [open]="showCreate()"
    [heading]="'branches.create.title' | t"
    (closed)="showCreate.set(false)"
  >
    <form [formGroup]="form" (ngSubmit)="create()" class="flex flex-col gap-4">
      <cs-input-field
        [label]="'field.name' | t"
        [control]="form.controls.name"
        [serverError]="fieldError('name')"
      />
      <cs-input-field
        [label]="'branches.region' | t"
        [control]="form.controls.region"
        [serverError]="fieldError('region')"
      />
      <cs-input-field
        [label]="'branches.timezone' | t"
        [control]="form.controls.timezone"
        [serverError]="fieldError('timezone')"
      />

      @if (createError(); as failure) {
        @if (!failure.hasFieldErrors) {
          <p class="text-body-md text-error" role="alert">{{ failure.message_ }}</p>
        }
      }

      <div class="flex items-center justify-end gap-2 border-t border-border-subtle pt-4">
        <cs-button type="submit" [busy]="saving()" [disabled]="form.invalid">
          {{ 'branches.create.submit' | t }}
        </cs-button>
      </div>
    </form>
  </cs-dialog>

  <cs-card [heading]="'branches.list.title' | t">
    @if (state().status === 'loading') {
      <cs-loading-state />
    } @else if (listError(); as failure) {
      <cs-error-state [error]="failure" />
    } @else if (state().status === 'empty') {
      <cs-empty-state [message]="'branches.empty' | t" />
    } @else {
      <div class="overflow-x-auto">
        <table class="w-full min-w-2xl text-body-md">
          <thead>
            <tr
              class="border-b border-border-subtle bg-surface-low text-label-md tracking-wider text-on-surface-variant uppercase"
            >
              <th scope="col" class="px-4 py-2 text-start">{{ 'field.name' | t }}</th>
              <th scope="col" class="px-4 py-2 text-start">{{ 'branches.region' | t }}</th>
              <th scope="col" class="px-4 py-2 text-start">{{ 'branches.timezone' | t }}</th>
              <th scope="col" class="px-4 py-2 text-start">{{ 'departments.state' | t }}</th>
              <th scope="col" class="px-4 py-2 text-end">{{ 'departments.actions' | t }}</th>
            </tr>
          </thead>
          <tbody>
            @for (branch of items(); track branch.id) {
              <tr
                class="border-b border-border-subtle transition-colors even:bg-surface-low last:border-transparent hover:bg-surface-high"
              >
                <td class="px-4 py-3 text-label-lg text-on-surface">{{ branch.name }}</td>
                <td class="px-4 py-3 text-on-surface-variant">{{ branch.region || '—' }}</td>
                <td class="px-4 py-3 text-on-surface-variant">{{ branch.timezone }}</td>
                <td class="px-4 py-3">
                  <span
                    class="inline-flex items-center gap-1 rounded px-2 py-0.5 text-label-md font-semibind"
                    [class]="branch.isActive ? 'bg-success/12 text-success' : 'bg-error/12 text-error'"
                  >
                    <span
                      class="size-1.5 shrink-0 rounded-full"
                      [class]="branch.isActive ? 'bg-success' : 'bg-error'"
                    ></span>
                    {{ (branch.isActive ? 'departments.active' : 'departments.deactivated') | t }}
                  </span>
                </td>
                <td class="px-4 py-3">
                  <div class="flex justify-end">
                    <cs-button
                      variant="secondary"
                      [disabled]="!branch.isActive"
                      (pressed)="deactivate(branch)"
                    >
                      {{ 'departments.deactivate' | t }}
                    </cs-button>
                  </div>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  </cs-card>
</section>
```

In `app.routes.ts`, add after the `sla-policies` route (line 73):
```ts
      {
        path: 'branches',
        // Same reasoning as 'departments': the guard here is a courtesy, the Admin policy on
        // /api/Branches' mutations is the control (FEAT-16, AC-120).
        canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/organisation/branches.component'),
      },
```
In `shell.component.ts`'s `NAV_ITEMS` (after the `departments` entry, line 53):
```ts
  { path: '/branches', key: 'nav.branches', icon: 'location_on', adminOnly: true },
```
In `translations.ts`, add under a `// ---- Branch administration ----` group (en/ar pairs, following
every neighbouring entry's `{ en, ar }` shape):
```ts
  'nav.branches': { en: 'Branches', ar: 'الفروع' },
  'branches.title': { en: 'Branches', ar: 'الفروع' },
  'branches.subtitle': { en: 'Manage branch records across the organisation.', ar: 'إدارة سجلات الفروع عبر المؤسسة.' },
  'branches.add': { en: 'Add branch', ar: 'إضافة فرع' },
  'branches.create.title': { en: 'New branch', ar: 'فرع جديد' },
  'branches.create.submit': { en: 'Create', ar: 'إنشاء' },
  'branches.list.title': { en: 'All branches', ar: 'كل الفروع' },
  'branches.empty': { en: 'No branches yet.', ar: 'لا توجد فروع بعد.' },
  'branches.region': { en: 'Region', ar: 'المنطقة' },
  'branches.timezone': { en: 'Timezone', ar: 'المنطقة الزمنية' },
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/branches.component.spec.ts'`
Expected: PASS, 3/3.

- [ ] **Step 5: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/organisation/branches.component.ts frontend/projects/admin-app/src/app/features/organisation/branches.component.html frontend/projects/admin-app/src/app/features/organisation/branches.component.spec.ts frontend/projects/admin-app/src/app/app.routes.ts frontend/projects/admin-app/src/app/layout/shell.component.ts frontend/projects/common/src/lib/i18n/translations.ts
git commit -m "feat(branches): admin screen, mirroring DepartmentsComponent (US-310 T2)"
```

## Definition of done

`AC-310.1` satisfied by the mini-spec section above (explicit OQ-5 scope exclusion), pending sign-off.
`AC-310.2` covered by Task 2's tests. Full frontend gate run once at the end, output pasted into the
task record:

```powershell
cd frontend
npx ng build admin-app
npx ng test common --watch=false
npx ng test admin-app --watch=false
```
