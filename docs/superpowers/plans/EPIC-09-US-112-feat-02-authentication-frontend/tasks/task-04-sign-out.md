# Task 4 — Sign out from the shell

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
| Criteria | AC-55 (part) |
| Status | `done` |

## Files

- `frontend/projects/admin-app/src/app/layout/shell.component.ts`
- `frontend/projects/admin-app/src/app/layout/shell.component.spec.ts`

## Test evidence

15 tests pass in `admin-app`. Full frontend suite 64 passing; `ng build admin-app` clean.

Run and observed, not assumed.

## Deviations from the plan

1. The test first used `signIn('a.b.c')` and failed: `isAuthenticated` is computed from decoded claims, so a placeholder token reads as unauthenticated. The spec now builds a structurally valid JWT - otherwise the assertion would have passed for the wrong reason.

## The point of this task

Clearing the token clears every session signal, because they are all computed over it.
