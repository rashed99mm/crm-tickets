# Task 2 — Identity registration, lockout and the sign-in use case

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
| Commit | `bdc48d3` |

## Files

- `src/CustomerSupport.Application/Common/Abstractions/IStaffAuthenticator.cs`
- `src/CustomerSupport.Application/Authentication/SignIn.cs`
- `src/CustomerSupport.Infrastructure/Identity/IdentityStaffAuthenticator.cs`
- `src/CustomerSupport.Infrastructure/Identity/IdentitySeeder.cs`
- `src/CustomerSupport.Infrastructure/Identity/IdentityServiceCollectionExtensions.cs`
- `tests/CustomerSupport.Application.Tests/Authentication/SignInHandlerTests.cs`

## Test evidence

56 Application tests pass, including 5 new handler tests.

Run and observed, not assumed. See the commit for the pasted suite output.

## Deviations from the plan

1. UserManager only, never SignInManager. SignInManager lives in the ASP.NET Core shared framework because it manages sign-in cookies, so referencing it would have required a FrameworkReference on Infrastructure and dragged the web framework into a layer that must stay framework-free (ruling R15). The lockout sequence is hand-rolled instead.
2. Handler registration became host-aware. An unfiltered MediatR scan registered the staff sign-in handler in the customer host too, and DI validation failed there. Registering staff identity in a customer-facing deployment to silence it would have let that deployment verify staff passwords - the wrong fix. CustomerApi now opts out via a MediatR TypeEvaluator.
3. Logging uses the LoggerMessage source generator: CA1848 is an error in this build.
4. InternalsVisibleTo added for the test assembly rather than widening handlers to public.

## The point of this task

IsLockedOutAsync is checked BEFORE the password, so a locked account is refused even with the right password. That ordering is what AC-6 actually asserts, and the obvious implementation gets it backwards.
