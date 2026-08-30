# MVP-03 / MVP-04 / MVP-05 — Customer workspace · **frontend** implementation plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Date:** 2026-08-26
**Spec:** [`../../specs/EPIC-02-US-001-customer-workspace.md`](../../specs/EPIC-02-US-001-customer-workspace.md)
**Criteria:** `AC-69`…`AC-75`
**Runs in parallel with:** [the backend plan](../EPIC-02-US-001-mvp-customer-workspace-backend/implementation-plan.md).
Disjoint files; the notes contract in the spec is fixed, so the notes UI is built against it before
the endpoint exists.

## Why this exists

There is **no customer UI at all**. Customers appear only as a `<select>` in the ticket create form.
The customer API has been built and tested since Phase 2 and is invisible in the product — the `G-5`
gap, raised and never closed.

## What to reuse, not rebuild

| Piece | Where | What it already does |
|---|---|---|
| `envelopeInterceptor` | `common/api` | Unwraps `data`, turns failures into `ApiError`, lowercases FluentValidation's PascalCase field keys |
| `ApiError.fieldError(name)` | `common/api` | The mechanism behind field-keyed errors |
| `CsInputField` | `common/ui` | Label, `aria-invalid`, `aria-describedby`; client errors after touch, **server errors immediately** |
| `AsyncState` + `CsLoadingState`/`CsEmptyState`/`CsErrorState` | `common` | The closed union that keeps empty and error distinct |
| `TicketQueueComponent` | `admin-app/features/tickets` | **The pattern to copy** for list + search + paging + three states |
| `TicketDetailComponent` | same | The pattern for detail + actions + a child timeline |

A second error-display path would put two definitions of "what a rejection looks like" in the
codebase. Compose what exists.

---

## Code plan

### T1 — `CustomerApi`

**New:** `frontend/projects/common/src/lib/customers/customer.api.ts` (+ `.spec.ts`), exported from
`public-api.ts`.

```ts
export interface Customer   { id: string; name: string; email: string;
                              phone: string | null; createdAt: string; }
export interface CustomerPage { items: readonly Customer[]; pageIndex: number;
                                pageSize: number; totalCount: number; }
export interface CustomerNote { id: string; body: string; authorId: string;
                                authorName: string; createdAt: string; }
export interface CustomerNotePage { items: readonly CustomerNote[]; pageIndex: number;
                                    pageSize: number; totalCount: number; }

@Injectable({ providedIn: 'root' })
export class CustomerApi {
  list(p: { page?: number; pageSize?: number; search?: string }): Observable<CustomerPage>
  get(id: string): Observable<Customer>
  create(r: { name: string; email: string; phone: string | null }): Observable<{ id: string }>
  update(id: string, r: {…}): Observable<unknown>
  remove(id: string): Observable<unknown>

  listNotes(id: string, page = 1, pageSize = 20): Observable<CustomerNotePage>
  addNote(id: string, body: string): Observable<{ id: string }>   // body only — AC-76
}
```

**Note the field is `pageIndex`, not `page`.** `PagedResult<T>` in `api-response.ts` declares `page`
and is wrong; `TicketPage` already works around it. Do not use `PagedResult<T>`.

`addNote` takes only `body`. There is no client-side author, so `AC-76` cannot be violated from here.

Catch nothing — failures must arrive as `ApiError`.

### T2 — Customer list · `AC-69`

**New:** `admin-app/src/app/features/customers/customer-list.component.{ts,html,spec.ts}`

Copy `TicketQueueComponent`'s shape exactly: `AsyncState<CustomerPage>` in a signal, `@switch` over
all five members, `CsLoadingState` / `CsEmptyState` / `CsErrorState`, previous/next paging.

```ts
readonly emptyMessage = computed(() =>
  this.search() ? 'No customers match that search.' : 'No customers recorded yet.');
```

That distinction is a criterion, not polish: "no customers" under an active search is a lie.

Rows link to `/customers/:id`. A header button links to `/customers/new`.

### T3 — Create a customer · `AC-70`

**New:** `customer-create.component.{ts,html,spec.ts}`

Typed non-nullable `FormGroup`: `name` (required, max 200), `email` (required, `Validators.email`,
max 320), `phone` (optional, max 32) — mirroring `CreateCustomerCommandValidator`.

Copy `TicketCreateComponent` for the submit guard, `fieldError(name)`, `clearServerError(name)` and
the form-level branch. **A duplicate email is a 409 with no field key**, so it must render at form
level — `formLevelError()` already handles exactly that case.

On success navigate to `/customers/:id`.

### T4 — Customer detail, edit, delete · `AC-71`, `AC-72`, `AC-73`

**New:** `customer-detail.component.{ts,html,spec.ts}`

- Route input `id` (`withComponentInputBinding()` is already enabled).
- Load in `queueMicrotask(() => this.load())` — the route input is not bound at construction, and an
  `effect` would re-fire on unrelated signal writes. `TicketDetailComponent` explains this.
- `AsyncState<Customer>`; 404 renders a not-found state, **not an empty form**.
- Inline edit form, same validators as create; a conflict renders at form level.
- Delete with a confirm. `AC-73`: on 409 (`CUSTOMER_HAS_TICKETS`) render the server's message and
  **leave the customer on screen** — do not navigate. On success go to `/customers`.
- Hosts `<admin-customer-notes [customerId]="id()" />`.

### T5 — Notes · `AC-74`, `AC-75`

**New:** `customer-notes.component.{ts,html,spec.ts}` — a child so `MVP-06` can add attachments
beside it without touching the profile.

- `AsyncState<CustomerNotePage>`, newest first as the server returns them (**do not re-sort in the
  client** — the order is a database index and re-sorting hides a server regression).
- Each entry: body, `authorName`, `createdAt`.
- Add box: a `textarea` + submit, disabled while empty/whitespace or in flight. **`AC-75` says an
  empty note is refused before any request is sent** — assert `http.expectNone(...)`.
- On success, re-read the list so the note appears without a page reload.

### T6 — Routes and navigation

**Edit:** `admin-app/src/app/app.routes.ts` — inside the guarded shell, and **`customers/new` before
`customers/:id`** or `new` matches as an id:

```ts
{ path: 'customers',     loadComponent: () => import('./features/customers/customer-list.component') },
{ path: 'customers/new', loadComponent: () => import('./features/customers/customer-create.component') },
{ path: 'customers/:id', loadComponent: () => import('./features/customers/customer-detail.component') },
```

**Edit:** `layout/shell.component.ts` — add a "Customers" nav link beside Tickets.

---

## Tests

`HttpTestingController` for every call: method, URL, body. Component tests named for their criteria:

| Test | Criterion |
|---|---|
| `AC69: lists customers with name, email and phone` | `AC-69` |
| `AC69: a failed load renders the error state, not an empty list` | `AC-69` |
| `AC69: an empty search says the search matched nothing` | `AC-69` |
| `AC70: a server field error appears under the control it names` | `AC-70` |
| `AC70: a duplicate email renders at form level, not on a field` | `AC-70` |
| `AC71: an unknown customer renders a not-found state` | `AC-71` |
| `AC72: saving a change posts it and re-reads` | `AC-72` |
| `AC73: a customer with tickets shows the refusal and stays on screen` | `AC-73` |
| `AC74: notes render newest first with author and time` | `AC-74` |
| `AC75: an empty note sends no request` | `AC-75` |
| `AC75: adding a note re-reads the list` | `AC-75` |

## Constraints

- **No physical-direction utilities** — `ms-`/`me-`, `ps-`/`pe-`, `text-start`/`text-end` only.
  `rtl-safety.spec.ts` scans every template and fails the build; it has caught one already.
- **Do not touch `backend/`.** The backend agent owns it.
- Every string is a plain literal for now — `MVP-13` introduces the dictionary and will convert them.

## Definition of done

`AC-69`…`AC-75` each covered by a test naming it · `npx ng test common --watch=false` and
`npx ng test admin-app --watch=false` green with output pasted · `npx ng build admin-app` clean ·
the charter in [`definition-of-done.md`](../../../requirements/mvp/definition-of-done.md) ·
task records in `tasks/`.
