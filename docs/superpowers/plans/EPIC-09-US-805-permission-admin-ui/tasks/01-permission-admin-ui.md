# T01 — Permission Administration API Consumer and Matrix UI

**Story:** `US-805`  
**Criteria:** `AC-805.1`, `AC-805.2`, `AC-805.3`, `AC-805.4`  
**Dependency:** `US-804`  
**Status:** implementation exists; verification pending

## Outcome recorded

The frontend and backend surface already exist. This task record documents what is present and
what must still be verified; it does not claim a shipped feature.

## Exact existing paths

### API and backend

- `backend/src/CustomerSupport.InternalApi/Controllers/PermissionsController.cs` —
  `[Route("api/admin/permissions")]`, API version `1.0`, and `[Authorize(Policy = "UserManagement")]`.
- `backend/src/CustomerSupport.Application/Features/Admin/Dtos/PermissionAdministrationDto.cs` —
  `Roles`, `Permissions`, role `PermissionIds`, and permission `Name`/`Description` records.
- `backend/src/CustomerSupport.Application/Interfaces/IPermissionAdministrationService.cs` —
  `GetAsync`, `AssignAsync`, `RevokeAsync`, and mutation result enum.
- `backend/src/CustomerSupport.Application/Features/Admin/Queries/GetPermissions/` — query and handler.
- `backend/src/CustomerSupport.Application/Features/Admin/Commands/AssignPermission/` — command,
  validator and handler.
- `backend/src/CustomerSupport.Application/Features/Admin/Commands/RevokePermission/` — command,
  validator and handler.
- `backend/src/CustomerSupport.Infrastructure/Security/PermissionAdministrationService.cs` —
  no-tracking list projection and assignment/revocation business rules.
- `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs` and
  `backend/src/CustomerSupport.Application/Messages/SystemCodeMap.cs` — stable error codes.
- `backend/src/CustomerSupport.Api.Shared/Middleware/AuthorizationEnvelopeMiddleware.cs` and
  `Extensions/ResponseExtensions.cs` — standard envelope and status mapping.

Representative DTO and application port:

```csharp
public sealed record PermissionAdministrationDto(
    IReadOnlyList<PermissionAdministrationRoleDto> Roles,
    IReadOnlyList<PermissionAdministrationPermissionDto> Permissions);

public interface IPermissionAdministrationService
{
    Task<PermissionAdministrationDto> GetAsync(CancellationToken ct = default);
    Task<PermissionMutationResult> AssignAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
    Task<PermissionMutationResult> RevokeAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
}
```

### Angular

- `frontend/projects/common/src/lib/admin/permission.api.ts` — typed client, root DI, and three HTTP methods.
- `frontend/projects/admin-app/src/app/features/admin/permissions.component.ts` — standalone
  `PermissionsComponent`, `inject(PermissionApi)`, signals and `AsyncState`.
- `frontend/projects/admin-app/src/app/features/admin/permissions.component.html` — matrix/table,
  loading/error/empty/success states, keyboard-reachable checkboxes and logical CSS utilities.
- `frontend/projects/admin-app/src/app/features/admin/permissions.component.spec.ts` — current
  `HttpTestingController` tests for initial load, matrix and mutation reload/error.
- `frontend/projects/admin-app/src/app/app.routes.ts` — lazy `permissions` route with
  `roleGuard('Admin')`.
- `frontend/projects/admin-app/src/app/layout/shell.component.ts` — Admin-only sidebar route.
- `frontend/projects/common/src/lib/i18n/translations.ts` — English/Arabic permission labels.

The route is:

```ts
{
  path: 'permissions',
  canActivate: [roleGuard('Admin')],
  loadComponent: () => import('./features/admin/permissions.component'),
}
```

The client is:

```ts
list(): Observable<PermissionAdministration> {
  return this.http.get<PermissionAdministration>('/api/admin/permissions');
}
assign(roleId: string, permissionId: string): Observable<unknown> {
  return this.http.post(`/api/admin/permissions/${roleId}/${permissionId}`, {});
}
revoke(roleId: string, permissionId: string): Observable<unknown> {
  return this.http.delete(`/api/admin/permissions/${roleId}/${permissionId}`);
}
```

## Required tests, named per AC

- `AC805_1_PermissionListRenders` — flush the envelope and assert one row, permission columns,
  and checked mapping; also assert loading and empty state are distinct.
- `AC805_1_PermissionListShowsVisibleError` — return a failed envelope and assert error/retry,
  not “no permissions”.
- `AC805_2_AssignPermissionToRole` — toggle an unchecked cell, assert POST route and success
  message, then flush GET and assert the server response drives the checked state.
- `AC805_3_RevokePermissionFromRole` — toggle a checked cell, assert DELETE route, success and
  reload; no optimistic local model mutation should be used.
- `AC805_4_CannotRemoveLastPermission` — return `409` with `PERMISSION_LAST_REQUIRED`, assert a
  visible warning, no success message, and a subsequent server reload retains the mapping.
- `AC805_4_ConcurrentLastPermissionRevokesLeaveOneMapping` — backend integration test using two
  real callers/transactions, proving the server cannot leave a built-in role with zero mappings.

The existing `permissions.component.spec.ts` currently has three unnamed tests covering initial
load/matrix, reload after mutation, and load failure. It does not prove AC-805.4, and its mutation
assertion observes the browser checkbox’s native click state before the response; that is not
equivalent to proving persisted state. Rename/supplement tests when implementation work is
authorized, then run them and paste output.

## Error and localization rules

The response is unwrapped only by `frontend/projects/common/src/lib/api`’s
`envelopeInterceptor`. Feature code receives `PermissionAdministration` or `ApiError`. For a
mutation error, render a localized safe warning using `permissions.mutationError` and, where the
error is specifically identified, a dedicated translated last-permission message. Do not expose
stack traces or raw server internals. Field errors are not expected for GUID route parameters;
400 remains the platform validation envelope.

## Last-permission guard and deviation

The current UI sends the revoke request and relies on the backend. The backend returns 409 for a
built-in role with one remaining mapping, but the UI has no dedicated warning assertion and the
template currently prints `failure.message_`. The current backend check is `CountAsync` followed by
`Remove`/`SaveChangesAsync`, which has a race between the count and delete. This task is therefore
not eligible for shipped status until the last-permission test and an atomic concurrency strategy
are evidenced. Do not fix that by weakening the server rule or by relying only on the Admin route
guard.

## Unit of Work note

The existing repository pattern is `IUnitOfWork` at
`backend/src/CustomerSupport.Domain/Interfaces/IUnitOfWork.cs`, implemented by
`backend/src/CustomerSupport.Infrastructure/Persistence/UnitOfWork.cs` and registered in
`RegisterPlatformInfrastructure`. The reference command boundary is:

```csharp
await repository.AddAsync(entity, ct);
await unitOfWork.SaveChangesAsync(ct);
```

Current `PermissionAdministrationService` uses `AppDbContext` directly. That is recorded as an
implementation deviation; this task record does not pretend it uses the Unit of Work.

## Status/evidence/deviation

- **Status:** partially verified 2026-08-27; frontend AC-named tests green, but **NOT shipped** —
  the atomic backend guard required for shipped status is skipped (see deviation).
- **Executed evidence (backend, US-804/805 shared):** `dotnet test ... --filter ...~Permission` →
  `Passed! Failed: 0, Passed: 13, Total: 13`. The suite was fixed: EF now discovers the
  `20260827100000_AddPermissions` migration (authoring its missing `.Designer.cs`), and the
  last-permission integration test now asserts the true wire code `ERR002` instead of the internal
  key `PERMISSION_LAST_REQUIRED`.
- **Executed evidence (frontend):** rewrote `permissions.component.spec.ts` with 5 AC-named tests —
  `AC805_1_PermissionListRenders`, `AC805_1_PermissionListShowsVisibleError`,
  `AC805_2_AssignPermissionToRole`, `AC805_3_RevokePermissionFromRole`,
  `AC805_4_CannotRemoveLastPermission`. Ran `npx ng test admin-app --watch=false --include="**/permissions.component.spec.ts"`
  → `5 passed / 5`; `npx ng build admin-app` → clean.
- **Frontend fix:** replaced raw `failure.message_` rendering with a localized safe warning — a
  dedicated `permissions.lastRequired` message (new translation) shown when the mutation error code
  is `ERR002` (the wire mapping of `PERMISSION_LAST_REQUIRED`), otherwise the generic
  `permissions.mutationError`. Component exposes `lastPermissionError` and the mutation error path
  never issues an optimistic model change or a reload.
- **Deviation (open):** the atomic backend guard and its race test
  (`AC805_4_ConcurrentLastPermissionRevokesLeaveOneMapping`) are **skipped on user instruction**.
  `PermissionAdministrationService.RevokeAsync` still performs count-then-delete, so two concurrent
  revokes could leave a built-in role with zero mappings. This is documented in the US-804 task
  record as an open defect; US-805 therefore does not claim shipped.

## Exact later commands

```powershell
cd backend
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~PermissionAdministration"
dotnet build CustomerSupport.slnx --warnaserror
cd ..\frontend
npx ng test admin-app --watch=false
npx ng build admin-app
```

For full evidence after focused tests:

```powershell
cd ..\backend
dotnet test CustomerSupport.slnx
```

Actual output belongs in this record. Until then, retain the story’s unshipped status.
