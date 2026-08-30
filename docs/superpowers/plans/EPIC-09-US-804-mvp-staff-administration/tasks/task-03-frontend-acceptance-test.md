# Task 3 — Frontend acceptance test (criterion 3, Angular half)

| Field | Value |
|---|---|
| Plan | [`implementation-plan.md`](../implementation-plan.md) — T3 |
| Story | `MVP-02` Administer staff accounts and roles |
| Criterion | 3 — "the staff screen is not offered" (the screen half) |
| Status | `done` |
| Commit | uncommitted — working tree |

## Deviation from the plan

The plan named `users.component.spec.ts` as the file to edit. It went into
`frontend/projects/admin-app/src/app/app.routes.spec.ts` instead: `roleGuard('Admin')` runs on the
route's `canActivate`, before the component is ever created, so a component spec's fixture never
reaches a state where the redirect is observable. `app.routes.spec.ts` already exercises the app's
real `Routes` array through a `Router` and carries the `jwtWithClaims`/`signIn` helpers this test
needed, so it is the file that can actually see a guard fire — and it already holds the equivalent
`AC69`/`AC70`/`AC71` routing tests this one now sits beside.

## What changed

`signIn()`'s hardcoded `'Admin'` role became a `role = 'Admin'` parameter, so a test can sign in as
any role without a second helper. Two tests were added:

- `MVP02: a non-admin visiting /users is sent to /forbidden, not the staff screen` — signs in as
  `Agent`, navigates to `/users`, asserts `router.url` is `/forbidden`.
- `MVP02: an admin visiting /users reaches the staff screen` — the positive case, so the first test
  is pinned to the role check and not to some other reason `/users` might fail to resolve.

## What this means for the story's status

Both halves of criterion 3 are now proven. The **backend** half —
`MVP02_NonAdmin_IsRefusedTheStaffSurface`, five `/api/Users` routes across four verbs as a
`Supervisor`, 403 from every one — remains the half that carries the security weight: a hidden
screen in front of an open endpoint would have protected nothing. The **screen** half closes the
courtesy: a non-admin who reaches `/users` directly is redirected before the screen's controls ever
render. `MVP-02` moves from `partial` to `done`.
