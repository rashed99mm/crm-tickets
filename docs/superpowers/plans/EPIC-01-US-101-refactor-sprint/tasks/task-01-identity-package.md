# T1 — Replace Identity EF package with pure Identity package

**AC:** AC-R1
**Status:** done — `Domain.csproj` carries no `PackageReference`; Identity's base classes resolve from the `Microsoft.AspNetCore.App` `FrameworkReference` already present, so no EF package is needed at all.

## What this task does

Removes `Microsoft.AspNetCore.Identity.EntityFrameworkCore` from `Domain.csproj` and replaces it with `Microsoft.AspNetCore.Identity` (the pure package without EF dependency). The base classes `IdentityUser<Guid>`, `IdentityRole<Guid>`, `IdentityUserClaim<Guid>`, `IdentityUserRole<Guid>`, `IdentityUserLogin<Guid>`, `IdentityUserToken<Guid>` all live in `Microsoft.AspNetCore.Identity`, not the EF package.

## Files to modify

- `backend/src/CustomerSupport.Domain/CustomerSupport.Domain.csproj` — swap package reference

## Verification

`dotnet build CustomerSupport.slnx` succeeds. The 6 Identity entity files continue to compile because their base classes come from the pure Identity package.
