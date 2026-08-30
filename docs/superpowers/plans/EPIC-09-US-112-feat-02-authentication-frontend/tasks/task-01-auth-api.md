# Task 1 — The sign-in API service

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

- `frontend/projects/common/src/lib/auth/auth.api.ts`
- `frontend/projects/common/src/lib/auth/auth.api.spec.ts`
- `frontend/projects/common/src/public-api.ts`

## Test evidence

46 tests pass in `common` (44 baseline + 2): method, url and body asserted via HttpTestingController, and the unwrapped result returned.

Run and observed, not assumed.

## Deviations from the plan

None.

## The point of this task

The service catches nothing. A rejection arrives as ApiError from the envelope interceptor and the component decides what to show; a service that swallowed it would have to invent a return value.
