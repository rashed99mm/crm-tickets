# Task 4 — Route the form and land the agent back on the queue

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — tasks 2.7, 2.8 |
| Feature | `FEAT-04` Ticket capture (frontend) |
| Criteria | `AC-55`, `AC-56` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `frontend/projects/admin-app/src/app/app.routes.ts`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.ts`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.html` (the entry link)

## Test evidence

- `navigates to the queue once the ticket is created`
- The existing `app.routes.spec.ts` still passes unchanged

`npx ng test admin-app --watch=false` — **41 passed**; `npx ng build admin-app` clean, with
`ticket-create-component` and `ticket-queue-component` emitted as separate lazy chunks.

## Deviations from the plan

**1. `tickets/new` is declared *before* the `tickets/:id` route that does not exist yet.**
`FEAT-06` adds the detail route. Angular matches routes in declaration order, so a later `:id`
placed above `new` would swallow it and try to load a ticket whose id is the string `"new"` — a bug
that appears in the *next* feature and looks like it belongs to that one. The ordering comment in
`app.routes.ts` says why, so whoever adds the detail route sees the constraint before breaking it.

**2. No route guard of its own.**
The route is a child of the shell, and `authGuard` sits on the parent. `AC-56` is satisfied by
inheritance — which is the arrangement the existing routing already chose deliberately, so that a
new child route is protected by default rather than by whoever adds it remembering to.

## The point of this task

Navigating to the queue on success rather than staying on a cleared form is what makes the create
feel finished: the agent sees the ticket they just raised in the list. It also means the queue is the
one screen that has to be right, which is `FEAT-05`.

`void this.router.navigateByUrl(...)` — the returned promise is deliberately discarded rather than
awaited. Nothing downstream depends on the navigation completing, and the `void` marks that as a
decision rather than an unhandled promise.
