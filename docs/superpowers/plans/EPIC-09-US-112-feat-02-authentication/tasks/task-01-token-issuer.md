# Task 1 — Mint access tokens behind a port

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
| Criteria | part of AC-1 |
| Status | `done` |
| Commit | `bbe70bc` |

## Files

- `src/CustomerSupport.Application/Common/Abstractions/ITokenIssuer.cs`
- `src/CustomerSupport.Infrastructure/Identity/JwtOptions.cs`
- `src/CustomerSupport.Infrastructure/Identity/JwtTokenIssuer.cs`
- `tests/CustomerSupport.Application.Tests/Authentication/JwtTokenIssuerTests.cs`

## Test evidence

5 tests pass: claim types, expiry, per-role claims, issuer/audience, and a no-roles user.

Run and observed, not assumed. See the commit for the pasted suite output.

## Deviations from the plan

1. The plan guessed Microsoft.IdentityModel.JsonWebTokens 8.14.0. Restore resolves 8.19.2 transitively from JwtBearer 10.0.11, so that is what is pinned. The plan instructed reading restore output rather than trusting its own number, and that paid off on the first step.
2. Tests use JsonWebTokenHandler rather than the plan's JwtSecurityTokenHandler, so the test project needs no second package.

## The point of this task

The claim-types test is the one that matters. HttpCurrentUser reads ClaimTypes.NameIdentifier and ClaimTypes.Name; a token issuing sub/name would compile, pass every test of its own logic, and attribute every audited row to "system".
