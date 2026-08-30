# Task 5 — Refuse bad tokens, enforce roles

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
| Feature | `FEAT-02` Authentication and session |
| Criteria | AC-3, part of AC-4 |
| Status | `done` |
| Commit | `61eee59` |

## Files

- `src/CustomerSupport.AdminApi/Program.cs`
- `tests/CustomerSupport.Api.IntegrationTests/Authentication/ProtectedEndpointTests.cs`

## Test evidence

7 tests pass.

Run and observed, not assumed. See the commit for the pasted suite output.

## Deviations from the plan

1. AC-4 is proven against environment-guarded probe endpoints, because S1 has no supervisor-only endpoint until customer delete in sprint 2. US-114 therefore closes this sprint partial.

## The point of this task

The foreign-signed token case earns its place: structurally perfect, right issuer and audience, even claiming Supervisor - the only case that fails if ValidateIssuerSigningKey is switched off.
