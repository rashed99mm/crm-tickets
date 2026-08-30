# Task 1 — The queue screen

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — tasks 3.1–3.2 |
| Feature | `FEAT-05` Ticket queue (frontend) |
| Criteria | `AC-57` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.ts`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.html`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.spec.ts`
- `frontend/projects/admin-app/src/app/app.routes.ts`
- **deleted:** `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.placeholder.ts`

## Test evidence

`AC57: renders the tickets returned by the api`.
`npx ng test admin-app --watch=false` — **41 passed (8 files)**; `npx ng build admin-app` clean.

## Deviations from the plan

**1. `text-left` was caught by the existing RTL-safety test.**
`rtl-safety.spec.ts` scans every template for physical-direction utilities and failed on this
template's table header. Corrected to `text-start`.

Worth recording: the guard fired on the **first template written after it was added**, which is the
argument for it existing. Physical-direction utilities break RTL silently — the layout looks correct
in English and mirrors wrongly in Arabic, so nobody notices until an Arabic speaker opens the app.

**2. The placeholder was deleted rather than left in place.**
`ticket-queue.placeholder.ts` existed only so `AC-55` had somewhere to land. Its own comment said it
should be deleted when the real queue arrived, so it was. No dead import remained and
`app.routes.spec.ts` still passes unchanged.

**3. Typed projection signals rather than template narrowing.**
Angular templates do not narrow a discriminated union across a `@switch`, so `tickets()`,
`totalCount()` and `listError()` project the payload-carrying cases into typed signals. Same
arrangement `users.component.ts` already uses — copied deliberately rather than invented.

## The point of this task

The list is an `AsyncState` union, not an array plus a loading flag. That choice is load-bearing and
belongs to task 3, but it is made here: modelling async work as "data or nothing" is what makes
`catchError(() => of([]))` look reasonable in the first place.

Rows are not clickable — `tickets/:id` arrives with `FEAT-06`. The `tickets/new` route is declared
before it will be, so `new` is not later matched as an id.
