# T01 — Permission Entity, Mapping, Seed and Policy

**Story:** `US-804`  
**Criteria:** `AC-804.1`, `AC-804.2`, `AC-804.3`  
**Status:** implementation exists; verification pending

## Outcome recorded

This task is recorded against the implementation already present. It was not implemented or
verified by this documentation change.

## Files and concrete implementation

Existing paths:

- `backend/src/CustomerSupport.Domain/Entities/Identity/Permission.cs` — private-set entity and `Permission.Create(string name, string? description = null)`; rejects whitespace/over-100 names and assigns `Guid.NewGuid()`/`DateTime.UtcNow`.
- `backend/src/CustomerSupport.Domain/Entities/Identity/RolePermission.cs` — private-set `RoleId`, `PermissionId`, navigation properties and `RolePermission.Create(Guid, Guid)`.
- `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs` — `Permissions` and `RolePermissions` DbSets on `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`.
- `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/PermissionConfiguration.cs` — `nvarchar(100)`, `nvarchar(500)`, `GETUTCDATE()`, unique `UQ_Permissions_Name`.
- `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/RolePermissionConfiguration.cs` — composite PK and cascade FKs.
- `backend/src/CustomerSupport.Infrastructure/Migrations/20260827100000_AddPermissions.cs` — schema `Up`/`Down`; join is dropped before permission in rollback.
- `backend/src/CustomerSupport.Infrastructure/Seeders/PermissionSeeder.cs` — ten-key catalogue and duplicate-safe role mapping.
- `backend/src/CustomerSupport.Api.Shared/Extensions/WebApplicationExtensions.cs` — internal startup invokes the seeder.
- `backend/src/CustomerSupport.Application/Interfaces/IPermissionService.cs` and `backend/src/CustomerSupport.Infrastructure/Security/PermissionService.cs` — application port and EF adapter.
- `backend/src/CustomerSupport.Api.Shared/Authorization/PermissionRequirement.cs` and `PermissionAuthorizationHandler.cs` — named requirement and user-role-permission lookup.
- `backend/src/CustomerSupport.Api.Shared/Extensions/AuthorizationExtensions.cs` — scoped handler and `UserManagement` policy (`Admin` + `user.manage`).
- `backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs` — scoped DI registrations.

Representative contract:

```csharp
public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, string permissionName,
        CancellationToken ct = default);
}
```

## Test-first verification to execute later

The intended failing-first tests are:

- `AC804_1_PermissionEntityHasRequiredFields` and `AC804_1_PermissionRejectsEmptyOrOverlongKey` in `backend/tests/CustomerSupport.Tests/Unit/Domain/PermissionTests.cs`.
- `AC804_2_RolePermissionMappingWorks` in `backend/tests/CustomerSupport.Tests/Integration/...` using the real EF context, not a mocked query provider.
- `AC804_3_PermissionsSeededOnStartup` and `AC804_3_PermissionsSeedIsIdempotent` in `backend/tests/CustomerSupport.Tests/Integration/...`.
- `AC804_PolicyRejectsMissingPermission` against a protected internal endpoint, asserting the standard forbidden envelope.

The repository currently has `PermissionEntityHasRequiredFields`,
`PermissionRejectsEmptyOrOverlongKey`, and `RolePermissionMapsRoleToPermission` in
`backend/tests/CustomerSupport.Tests/Unit/Domain/PermissionTests.cs`; their names do not yet
contain the stable AC identifier and the mapping test is not an integration test. This is an
evidence gap, not a passing claim.

## Seed and rollback checklist

1. Apply `20260827100000_AddPermissions` after Identity roles exist.
2. Run internal-host startup: `IdentitySeeder`, then `PermissionSeeder`.
3. Repeat startup and assert no duplicate permission names or role joins.
4. For rollback, target the preceding migration; inspect that `Down` drops `RolePermissions` then `Permissions` and acknowledge data loss.
5. Do not seed from `CustomerSupport.ExternalApi`.

## Unit of Work note

The available pattern is `IUnitOfWork` at
`backend/src/CustomerSupport.Domain/Interfaces/IUnitOfWork.cs`, implemented by
`backend/src/CustomerSupport.Infrastructure/Persistence/UnitOfWork.cs` and registered scoped.
The current seeder and permission administration adapter call `AppDbContext.SaveChangesAsync`
directly. That observed deviation must not be described as handler-level Unit of Work usage.

## Status/evidence/deviation

- **Status:** verified 2026-08-27; all 13 permission tests pass (`dotnet test ... --filter ...~Permission` → `Passed! Failed: 0, Passed: 13, Total: 13`).
- **Executed evidence:** the permission suite was red at five passing tests because **EF did not
  discover the `20260827100000_AddPermissions` migration** — it had no `.Designer.cs` companion, so
  the `[DbContext]`/`[Migration]` attributes EF scans for were absent and the `Permissions`/
  `RolePermissions` tables were never created. Fixed by authoring
  `20260827100000_AddPermissions.Designer.cs` (target model = current snapshot) and removing the
  duplicate `[Migration]` attribute from the main file. After applying the migration the suite went
  green.
- **Test defect fixed:** `LastPermissionOnBuiltInRoleIsRejected` asserted the internal key
  `PERMISSION_LAST_REQUIRED` appears in the envelope, but the wire contract maps it to the stable
  code `ERR002` (`SystemCodeMap.cs`) with a localized message. Corrected to assert `code = ERR002`.
- **Concurrency deviation (documented, NOT resolved):** permission revoke performs count-then-delete
  (`PermissionAdministrationService.RevokeAsync`), so the "last permission on a built-in role" rule is
  not an atomic concurrency guard — two concurrent revokes could each observe count > 1 and both
  delete, leaving zero permissions. An atomic guard (isolated transaction / conditional delete) was
  prototyped but is **skipped** on user instruction for now; no concurrency race test ships and
  US-805 does **not** claim shipped. This remains an open defect.

## Exact later commands

```powershell
dotnet build backend/CustomerSupport.slnx --warnaserror
dotnet ef database update --project backend/src/CustomerSupport.Infrastructure --startup-project backend/src/CustomerSupport.InternalApi
```

Canonical full suite, when authorized:

```powershell
cd backend
dotnet build CustomerSupport.slnx
dotnet test CustomerSupport.slnx
```

Paste actual output here after execution.
