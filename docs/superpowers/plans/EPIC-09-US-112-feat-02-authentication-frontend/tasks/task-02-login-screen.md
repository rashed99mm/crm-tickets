# Task 2 — The sign-in screen

> **Superseded 2026-08-25 by the platform baseline.** The backend this document describes was
> replaced when the CCE Platform reference was adopted as the CRM baseline — see
> [`EPIC-12-US-000-crm-platform-baseline-design.md`](../../../specs/EPIC-12-US-000-crm-platform-baseline-design.md).
> The code named below no longer exists in `src/`; it is archived, not deleted. This file is kept
> because it is the record of what was built and why, and deleting it would erase the reasoning
> behind decisions that still hold — the envelope, the localisation approach and the dependency rule
> among them. **Do not follow its steps.**


| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) |
| Feature | `FEAT-02` Authentication and session (frontend half) |
| Criteria | AC-55 |
| Status | `done` |

## Files

- `frontend/projects/admin-app/src/app/features/auth/login.component.ts`
- `frontend/projects/admin-app/src/app/features/auth/login.component.spec.ts`

## Test evidence

10 tests pass in `admin-app`.

Run and observed, not assumed.

## Deviations from the plan

1. The plan's spec used `provideHttpClient()` with no interceptors, so the component fell through to its unknown-error path and asserted a fiction production never sees. The spec now runs the real `envelopeInterceptor`, and the success case flushes a full envelope so unwrapping is exercised too.
2. `LocaleStore` resolves a bilingual pair via `resolve()`, not the `text()` the plan guessed. The plan said to match what exists rather than force its own naming.

## The point of this task

The load-bearing assertion is the negative one: `navigateByUrl` was NOT called on a rejected sign-in. A form that navigates and bounces back flashes the protected page.
