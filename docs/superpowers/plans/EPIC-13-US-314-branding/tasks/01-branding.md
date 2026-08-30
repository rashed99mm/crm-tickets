# T1 — Branding Contract, Tenant Gate, and Failing Tests

**Story:** `US-314`  
**Criteria:** AC-314.1, AC-314.2, AC-314.3; original AC-25  
**Status:** not started  
**Commit:** pending  
**Test evidence:** none; not run by instruction

## Gate

Confirm the authoritative tenant identity before changing `PlatformSetting`. The current
`PlatformSettings` table is global (`Key` primary key), while the approved spec requires tenant
isolation. If `IUserContext`/claims do not identify a tenant, create an ADR/blocker and stop; do
not use `BranchId` or a client-provided key prefix as a substitute.

## Files to inspect and then change after the gate

- `backend/src/CustomerSupport.Domain/Entities/PlatformSettings/PlatformSetting.cs`;
- `backend/src/CustomerSupport.Application/Interfaces/IUserContext.cs`;
- `backend/src/CustomerSupport.InternalApi/Controllers/PlatformSettingsController.cs`;
- `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/`;
- `frontend/projects/common/src/lib/admin/platform-setting.api.ts`;
- `frontend/projects/common/src/styles/theme.css`;
- `frontend/projects/admin-app/src/app/layout/shell.component.{ts,html}`.

## Work

1. Add failing backend tests named `AC314_1_GetBrandingReturnsCurrentTenant`,
   `AC314_1_UpdateBrandingRequiresAdmin`, `AC314_2_InvalidColorUrlAndAssetAreRejected`, and
   `AC314_3_CrossTenantBrandingIsNotReadable`.
2. Add failing frontend tests named `AC314_3_BrandStoreAppliesCssVariables`,
   `AC314_3_DefaultBrandingSurvivesLoadFailure`, and `AC314_3_LogoHasAccessibleAlternativeText`.
3. Specify exact 200/400/401/403/404 envelope responses, validation limits, asset policy, safe
   defaults, update audit, and whether reads are authenticated or public.
4. Only after approval, implement a typed branding endpoint/store and Admin editor; never expose
   arbitrary platform settings or accept tenant id in the request.

## Later verification

```powershell
cd backend
dotnet test CustomerSupport.Tests/CustomerSupport.Tests.csproj --filter "FullyQualifiedName~Branding"
dotnet test CustomerSupport.slnx
dotnet build CustomerSupport.slnx --warnaserror
cd ..\frontend
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
npx playwright test --grep "branding|logo"
```

## Evidence / deviations

**Evidence:** pending tenant decision, failing tests, implementation output, and cross-tenant proof.  
**Deviations:** none. Do not mark complete with only CSS variables or a global setting update; all
three spec criteria require the secured API, validation, tenant isolation, and UI behavior.
