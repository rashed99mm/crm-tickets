# Task 4 — POST /api/auth/sign-in

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
| Criteria | AC-1, AC-2, AC-6, AC-67 |
| Status | `done` |
| Commit | `4257f7a` |

## Files

- `src/CustomerSupport.AdminApi/Endpoints/AuthEndpoints.cs`
- `src/CustomerSupport.AdminApi/Program.cs`
- `tests/CustomerSupport.Api.IntegrationTests/AdminApiFactory.cs`
- `tests/CustomerSupport.Api.IntegrationTests/Authentication/SignInEndpointTests.cs`

## Test evidence

6 endpoint tests pass against a real SQL Server database and the composed pipeline.

Run and observed, not assumed. See the commit for the pasted suite output.

## Deviations from the plan

1. A class fixture never had IAsyncLifetime.InitializeAsync invoked, so the database was never migrated and every test failed on "Cannot open database". The test class now owns the factory.
2. MigrateAsync cannot create a database the connection string already names - SqlClient fails at login first - so the database is created through a master connection first.
3. Migration must not use a context resolved from Services: touching Services starts the host, and the host seeds identity during startup, so the seeder ran against a schema that did not exist yet ("Invalid object name AspNetRoles").

## The point of this task

Two assertions carry the weight. Unknown-email and wrong-password responses are compared field by field and must be identical. The lockout test then signs in with the CORRECT password and requires a refusal - asserting a fourth wrong password fails would prove nothing.
