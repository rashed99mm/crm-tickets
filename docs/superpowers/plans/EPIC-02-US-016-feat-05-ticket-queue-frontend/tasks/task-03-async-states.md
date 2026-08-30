# Task 3 — Loading, empty and error, kept distinct

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — US-126, tasks 4.1–4.4 |
| Feature | `FEAT-05` Ticket queue (frontend) |
| Criteria | `AC-58` |
| Status | `done` — with the coverage note below |
| Commit | uncommitted — working tree |

## Files

- `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.ts`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.html`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.spec.ts`
- reused: `common/state/async-state.ts`, `common/ui/{loading,empty,error}-state.component.ts`

## Test evidence

- `AC58: shows a loading state while the request is in flight`
- `AC58: a failed request renders the error state, never the empty state`
- `AC58: a successful empty result renders the empty state, with no retry offered`
- `AC58: retrying re-issues the request`

`npx ng test admin-app --watch=false` — **41 passed**.

## The pair of tests that carry the criterion

They assert the two halves of a distinction that collapses by accident. The default mistake is one
line:

```ts
catchError(() => of([]))
```

That turns a 500 into "no tickets". The agent reports missing work, nobody looks for a server fault
because the UI said there was nothing to show, and the outage stays invisible.

- **`a failed request renders the error state, never the empty state`** asserts the error text is
  present **and** the empty-state copy is absent.
- **`a successful empty result … with no retry offered`** asserts the retry button is absent.

The retry button's presence in one and absence in the other is both the honest signal — nothing
failed, so there is nothing to retry — and the visual difference that stops the two reading alike.

## Why this is structural, not a matter of remembering

`empty()` is reachable **only from the success callback**. The error callback can only produce
`failed(...)`. So there is no code path by which a failure becomes an empty list, and the guarantee
survives someone editing this component without reading this file.

## Coverage note — `US-126` TC-04 is `PARTIAL`

`AC-58` says loading, empty and error are distinct **on every data view**. There is currently one
data view: the ticket queue. Ticket detail arrives with `FEAT-06`, and the customer screens are cut.

The shared `AsyncState` union and the three state components make the guarantee structural rather
than per-screen, but it is **demonstrated on one view, not three**. That is as complete as it can be
today, and `TC-04` says so rather than claiming three.

## Deviations from the plan

None. Tasks 4.1–4.4 landed as written — largely because `common` already carried the union and the
three components from `FEAT-02`, so this task composed rather than built.
