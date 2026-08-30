# FEAT-05 Ticket Queue (frontend) Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Goal:** The queue screen — paged list, status filter, "my tickets" toggle, and three visually distinct states (loading / empty / error) so a server outage never renders as "no tickets" (`AC-57`, `AC-58`, `US-038`, `US-126`).

**Spec:** `docs/superpowers/specs/EPIC-02-US-016-ticket-lifecycle.md` (`AC-57`, `AC-58`).

**Architecture:** `ticket-queue.component.ts` (admin-app) → `TicketApi.list`. The list is an `AsyncState<PagedResult<TicketListItem>>` discriminated union, which is the whole of `AC-58`'s defence (`catchError(() => of([]))` is the default mistake this forbids).

## Global constraints

- The list is an `AsyncState` union — never `array + loadingFlag` — so an error cannot collapse into an empty list.
- `empty()` is only ever set from a *successful* request that returned nothing; an error sets `failed()`.
- Status filter is sent only when set; `mine`/`unassigned` only when true (`ticket.api` omits falsey flags so the server doesn't 400 on a blank value).

## Task 1 — `TicketApi.list` filters (`AC-57`)

**Files:** `frontend/projects/common/src/lib/tickets/ticket.api.ts` (`list`, `TicketFilters`)

**Step 1 — Real API (excerpt)**

```ts
export interface TicketFilters {
  readonly page?: number; readonly pageSize?: number;
  readonly status?: TicketStatus | null; readonly mine?: boolean; readonly unassigned?: boolean;
}
@Injectable({ providedIn: 'root' })
export class TicketApi {
  list(filters: TicketFilters = {}): Observable<PagedResult<TicketListItem>> {
    let params = new HttpParams()
      .set('page', String(filters.page ?? 1)).set('pageSize', String(filters.pageSize ?? 10));
    if (filters.status) params = params.set('status', filters.status);   // absent, not empty
    if (filters.mine) params = params.set('mine', 'true');
    if (filters.unassigned) params = params.set('unassigned', 'true');
    return this.http.get<PagedResult<TicketListItem>>('/api/Tickets', { params });
  }
}
```

- [ ] **Step 2: Run:** `cd frontend && npx ng test common --watch=false --filter ticket.api`
Expected: PASS — `list({status:'Open', mine:true})` sends both params; `list()` sends neither.

- [ ] **Step 3: Commit:** `git add frontend/projects/common/src/lib/tickets/ticket.api.ts && git commit -m "feat(queue-fe): TicketApi.list filters (AC-57)"`

## Task 2 — Queue component: AsyncState + 3 states (`AC-58`)

**Files:** `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.ts`

**Step 1 — Real component (excerpt)**

```ts
export default class TicketQueueComponent {
  readonly state = signal<AsyncState<PagedResult<TicketListItem>>>(loading());
  readonly status = signal<TicketStatus | null>(null);
  readonly mine = signal(false);
  readonly page = signal(1);

  constructor() { this.load(); }

  load(): void {
    this.state.set(loading());
    this.api.list({ page: this.page(), pageSize: 10, status: this.status(), mine: this.mine() }).subscribe({
      next: (result) => this.state.set(result.items.length === 0 ? empty() : loaded(result)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  readonly listError = computed<ApiError | null>(() => {
    const c = this.state(); return c.status === 'error' ? c.error : null;
  });
  readonly tickets = computed<readonly TicketListItem[]>(() => {
    const c = this.state(); return c.status === 'loaded' ? c.data.items : [];
  });
}
```

`empty()` is only reached from the `next` branch (a successful empty page); the `error` branch sets `failed()`. The template switches on `state().status` to render `CsLoadingState` / `CsEmptyState` / `CsErrorState` distinctly (`AC-58`).

- [ ] **Step 2: Run:** `cd frontend && npx ng test admin-app --watch=false --filter ticket-queue`
Expected: PASS — loading/empty/error each render distinctly; a forced 500 sets `failed()`, never `empty()`.

- [ ] **Step 3: Commit:** `git add frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.ts && git commit -m "feat(queue-fe): queue AsyncState, 3 states (AC-58)"`

## Task 3 — Route wiring

`app.routes.ts`: `tickets` → `ticket-queue.component` inside the `authGuard` shell. No role guard — the queue is for any authenticated staff.

- [ ] **Step 1: Run:** `cd frontend && npx ng build admin-app`
Expected: clean build.

- [ ] **Step 2: Commit:** `git add frontend/projects/admin-app/src/app/app.routes.ts && git commit -m "feat(queue-fe): queue route"`

## Self-review

Coverage: `AC-57` → Task 1; `AC-58` → Task 2.

**Discrepancy found:** the old plan warned `catchError(() => of([]))` "is the default mistake here". The shipped `load()` uses `failed(this.toApiError(error))`, which is exactly the correction — no gap, and the rewrite names the real `AsyncState` union as the mechanism.
