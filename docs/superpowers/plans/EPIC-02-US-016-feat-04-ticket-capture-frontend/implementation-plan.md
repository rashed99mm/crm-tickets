# FEAT-04 Ticket Capture (frontend) Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Goal:** The create-ticket form — typed reactive form whose client rules mirror the server's, and whose server `errors[]` land on the control named by `field` (`AC-59`, `AC-60`, `US-127`).

**Spec:** `docs/superpowers/specs/EPIC-02-US-016-ticket-lifecycle.md` (`AC-59`, `AC-60`).

**Architecture:** `ticket-create.component.ts` (admin-app) → `TicketApi` / `CustomerApi` (common). The envelope interceptor turns a field-keyed `FieldError` into an `ApiError`; the component reads `submitError().fieldError(field)`.

## Global constraints

- Client rules mirror the server's (`CreateTicketCommandValidator` says 200 chars) — where they disagree, the server wins and `AC-60`'s path shows why.
- A failure with no field renders at form level, not on a control.
- The create form is the vertical slice's proof the `errors[]` contract is consumable (see `two-day-completion`).

## Task 1 — `TicketApi` create + pickers (`AC-59`, `AC-60`)

**Files:**
- `frontend/projects/common/src/lib/tickets/ticket.api.ts`
- `frontend/projects/common/src/lib/customers/customer.api.ts`

**Interfaces:** `TicketApi.create(req: CreateTicketRequest): Observable<{id:string}>`, `listCategories()`, `searchCustomers(search)`.

**Step 1 — Real API (excerpt)**

```ts
// frontend/projects/common/src/lib/tickets/ticket.api.ts
export interface CreateTicketRequest {
  readonly subject: string; readonly description: string;
  readonly customerId: string; readonly categoryId: string; readonly priority: TicketPriority;
}
@Injectable({ providedIn: 'root' })
export class TicketApi {
  private readonly http = inject(HttpClient);
  create(request: CreateTicketRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/Tickets', request);
  }
  listCategories(): Observable<readonly CategoryOption[]> {      // seeded four-bucket list
    return this.http.get<readonly CategoryOption[]>('/api/Categories');
  }
  searchCustomers(search: string): Observable<{ items: readonly CustomerOption[] }> {
    const params = new HttpParams().set('pageSize', '20').set('search', search);
    return this.http.get<{ items: readonly CustomerOption[] }>('/api/Customers', { params });
  }
}
```

The customer picker reuses `CustomerApi.list` (paged, search) — the same endpoint the customer list screen uses.

- [ ] **Step 2: Run:** `cd frontend && npx ng test common --watch=false --filter ticket.api`
Expected: PASS — `create` posts to `/api/Tickets`; `listCategories` hits `/api/Categories`.

- [ ] **Step 3: Commit:** `git add frontend/projects/common/src/lib/tickets/ticket.api.ts && git commit -m "feat(tickets-fe): TicketApi create + pickers (AC-59)"`

## Task 2 — Create form: client rules + field errors (`AC-59`, `AC-60`)

**Files:** `frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.ts`

**Step 1 — Real component (excerpt)**

```ts
export default class TicketCreateComponent {
  readonly form = new FormGroup({
    subject: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    description: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    customerId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    categoryId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    priority: new FormControl<TicketPriority>('Normal', { nonNullable: true, validators: [Validators.required] }),
  });
  readonly submitError = signal<ApiError | null>(null);

  readonly formLevelError = computed(() => {
    const f = this.submitError();
    return f && !f.hasFieldErrors ? f : null;       // no-field failure -> form level
  });

  submit(): void {
    if (this.form.invalid || this.saving()) { this.form.markAllAsTouched(); return; }
    this.saving.set(true); this.submitError.set(null);
    this.api.create(this.form.getRawValue()).subscribe({
      next: () => { this.saving.set(false); void this.router.navigateByUrl('/tickets'); },
      error: (error: unknown) => this.submitError.set(this.toApiError(error)),
    });
  }

  fieldError(field: string) { return this.submitError()?.fieldError(field) ?? null; }  // AC-60

  clearServerError(field: string): void {            // clear once the user edits the control
    const f = this.submitError();
    if (!f?.fieldError(field)) return;
    this.submitError.set(new ApiError(f.code, f.message_,
      f.errors.filter((e) => e.field !== field), f.traceId, f.status));
  }
}
```

`fieldError('customerId')` returns the server's `FieldError` for that control because the envelope interceptor lower-cased the PascalCase `CustomerId` into `customerId` (`toControlName`).

- [ ] **Step 2: Run:** `cd frontend && npx ng test admin-app --watch=false --filter ticket-create`
Expected: PASS — unknown `customerId` returned by the server binds to the `customerId` control; a non-field failure shows at form level.

- [ ] **Step 3: Commit:** `git add frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.ts && git commit -m "feat(tickets-fe): create form, server errors[] on control (AC-59, AC-60)"`

## Task 3 — Route wiring

**Files:** `frontend/projects/admin-app/src/app/app.routes.ts` — `tickets/new` declared **before** `tickets/:id` so `new` is not captured as an id.

- [ ] **Step 1: Run:** `cd frontend && npx ng build admin-app`
Expected: build clean; `/tickets/new` lazy-loads the create component.

- [ ] **Step 2: Commit:** `git add frontend/projects/admin-app/src/app/app.routes.ts && git commit -m "feat(tickets-fe): route tickets/new before :id"`

## Self-review

Coverage: `AC-59` → Tasks 1,2; `AC-60` → Task 2.

**Discrepancy found:** the old plan's task 1.8 ("server `errors[]` land on the control named by `field`") is exactly what shipped — no gap. The customer picker reuses `CustomerApi.list`, not a dedicated endpoint, which the old prose implied as a separate "customer search" wire; the rewrite states the real reuse.
