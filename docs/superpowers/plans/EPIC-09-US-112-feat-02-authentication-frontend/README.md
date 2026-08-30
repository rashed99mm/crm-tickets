# FEAT-02 frontend — execution record

> **Superseded 2026-08-25 by the platform baseline.** The backend this document describes was
> replaced when the CCE Platform reference was adopted as the CRM baseline — see
> [`EPIC-12-US-000-crm-platform-baseline-design.md`](../../specs/EPIC-12-US-000-crm-platform-baseline-design.md).
> The code named below no longer exists in `src/`; it is archived, not deleted. This file is kept
> because it is the record of what was built and why, and deleting it would erase the reasoning
> behind decisions that still hold — the envelope, the localisation approach and the dependency rule
> among them. **Do not follow its steps.**


Per-task records for
[`implementation-plan/implementation-plan.md`](./implementation-plan.md).
The backend half has [its own record](../EPIC-09-US-112-feat-02-authentication/README.md).

| Task | Title | Criteria | Status |
|---|---|---|---|
| [01](./tasks/task-01-auth-api.md) | The sign-in API service | AC-55 (part) | `done` |
| [02](./tasks/task-02-login-screen.md) | The sign-in screen | AC-55 | `done` |
| [03](./tasks/task-03-routes-and-guard.md) | Routes and the guard | AC-56 | `done` |
| [04](./tasks/task-04-sign-out.md) | Sign out from the shell | AC-55 (part) | `done` |

Plan task 5 (records and status) is this folder plus the documentation commit.

## FEAT-02 is shipped

Backend, frontend and tests, together - this project's definition of shipped.

Verified 2026-08-25: **240 backend tests** (139 domain, 56 application, 45 integration) and
**64 frontend tests** (46 `common`, 15 `admin-app`, 3 `portal-app`), 0 failing, clean build with
warnings as errors and a clean `ng build admin-app`.

| Criterion | Status |
|---|---|
| `AC-1`, `AC-2`, `AC-3`, `AC-5`, `AC-6`, `AC-67` | `done` (backend half) |
| `AC-55`, `AC-56` | `done` |
| `AC-4` | **`partial`** — policy proven against guarded probes; needs `US-117`'s supervisor-only endpoint |

## What each deviation taught

All three deviations were the same mistake in different clothes: **a test that passes because its
setup differs from production is worse than no test.**

1. The login spec omitted the envelope interceptor, so the component took its unknown-error path and
   the assertion passed against a code path production never runs.
2. The shell spec signed in with `'a.b.c'`, which decodes to no claims, so `isAuthenticated` was
   false and the test would have passed for the wrong reason.
3. Two API names in the plan (`LocaleStore.text`, `ShellComponent`) did not exist. The plan told the
   implementer to match what exists rather than force its own naming, and that instruction is why
   these were caught in seconds rather than argued with.

## Scope boundaries recorded, not hidden

- **`/tickets` is a placeholder.** `AC-55` asserts navigation and the test asserts the *route*, so
  `FEAT-05` replaces the component without touching these tests. It says on screen that the queue
  arrives with `FEAT-05` rather than rendering an empty table that would read as a broken feature.
- **No Playwright coverage.** `AC-64` is `FEAT-11`, terminal by design.
- **The shell still links to `/customers`, which has no screen.** That is gap `G-5`: the spec defines
  no frontend criterion for customer management. The link currently falls through the wildcard to
  `/tickets`. Left as-is rather than silently deleting a nav item the mockups show.

## Next

`FEAT-03` (customer records, API-only per `G-5`), then `FEAT-04` (ticket capture) — the highest-value
early feature, because its form is the first thing that proves the validation contract is consumable
end to end.
