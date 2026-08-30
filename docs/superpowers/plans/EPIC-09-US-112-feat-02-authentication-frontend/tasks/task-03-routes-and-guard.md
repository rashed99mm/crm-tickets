# Task 3 — Routes and the guard

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
| Criteria | AC-56 |
| Status | `done` |

## Files

- `frontend/projects/admin-app/src/app/app.routes.ts`
- `frontend/projects/admin-app/src/app/app.routes.spec.ts`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.placeholder.ts`
- `frontend/projects/admin-app/src/app/features/errors/forbidden.component.ts`

## Test evidence

14 tests pass in `admin-app`; all four route tests were red first.

Run and observed, not assumed.

## Deviations from the plan

1. The shell exports `AdminShell`, not the `ShellComponent` the plan assumed.

## The point of this task

The guard sits on the parent route, so a new child route is protected by default rather than by whoever adds it remembering to. `/forbidden` exists because `roleGuard` already navigated there - the foundation shipped a guard pointing at a route that did not.
