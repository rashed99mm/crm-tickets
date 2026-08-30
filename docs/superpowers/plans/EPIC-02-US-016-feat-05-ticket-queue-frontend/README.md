# FEAT-05 — Ticket queue · frontend task record

**Plan:** [`implementation-plan/implementation-plan.md`](./implementation-plan.md)
**Executed:** 2026-08-26
**Status:** delivered

## Evidence

```
npx ng test admin-app --watch=false
 Test Files  8 passed (8)
      Tests  41 passed (41)

npx ng build admin-app
Application bundle generation complete. [4.674 seconds]
```

## Tasks

| # | Task | Criteria | Commit | Status |
|---|---|---|---|---|
| [01](./tasks/task-01-queue-component.md) | The queue screen | AC-57 | uncommitted | `done` |
| [02](./tasks/task-02-filters-and-paging.md) | Status filter, mine toggle, paging | AC-57 | uncommitted | `done` |
| [03](./tasks/task-03-async-states.md) | Loading, empty and error kept distinct | AC-58 | uncommitted | `done` |

## Criteria delivered

| `AC-n` | Test naming it |
|---|---|
| AC-57 | `AC57: renders the tickets returned by the api`, `AC57: the status filter refetches with the selected status`, `AC57: the my-tickets toggle requests only the caller's own work` |
| AC-58 | `AC58: shows a loading state while the request is in flight`, `AC58: a failed request renders the error state, never the empty state`, `AC58: a successful empty result renders the empty state, with no retry offered`, `AC58: retrying re-issues the request` |
| — | `says the filter matched nothing, rather than claiming the queue is empty` |

## The pair of tests that carry AC-58

They assert the two halves of a distinction that collapses by accident:

```ts
catchError(() => of([]))   // the default mistake
```

That line turns a 500 into "no tickets". The user reports missing work, nobody looks for a server
fault because the UI said there was nothing to show, and the outage stays invisible.

- **`a failed request renders the error state, never the empty state`** asserts the error text is
  present *and* the empty-state copy is absent.
- **`a successful empty result … with no retry offered`** asserts the retry button is absent.

The retry button's presence in one and absence in the other is both the honest signal — nothing
failed, so there is nothing to retry — and the visual difference that stops the two reading alike.
`fromList`/`empty()` are only ever reached from the success callback, which is what makes the
distinction structural rather than a matter of remembering.

## Deviations from the plan

**D1 — `PagedResult<T>` in `api-response.ts` was left alone; a correct type was defined alongside it.**
The plan's task 3.1 said to fix `page` → `pageIndex` there. Confirmed against a real response that
the backend serialises `pageIndex` — but `PagedResult<T>` is exported from the common library's
public API and has no current consumer, so editing it would change a published type to fix a
consumer that does not exist. `TicketPage` in `ticket.api.ts` declares the true shape and carries a
comment pointing at the stale one. **The divergence is now documented in two places instead of one,
which is worse than fixing it** — the reason it was not fixed is that verifying no other consumer
exists is a sweep this budget did not have. Flagged for `FEAT-09`'s contract-hardening pass.

**D2 — Paging is previous/next, not numbered pages.**
`hasMore()` derives from `page * 10 < totalCount`, with a hardcoded page size of 10 that matches the
request. It is duplicated knowledge — changing the page size means changing two places — and a
numbered pager reading `pageSize` from the response would be better. Left as is for the budget.

**D3 — The placeholder was deleted, as planned.**
`ticket-queue.placeholder.ts` is gone and the route points at the real component. No dead import
remained; `app.routes.spec.ts` still passes.

## Not done

**No detail route yet.** Rows are not clickable — `tickets/:id` arrives with `FEAT-06`. The
`tickets/new` route is declared *before* it will be, so `new` is not later matched as an id.
