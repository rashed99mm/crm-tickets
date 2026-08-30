# Task 11 - Platform Structure Branding

**Status:** Ready  
**Closes gaps:** Multi-branch UI, multi-team UI, branding miswire, runtime branding, logo upload, global language default, department tree.

## Files

- Backend: `BranchesController.cs`, `TeamsController.cs`, `DepartmentsController.cs`, `PlatformSettingsController.cs`
- Frontend API: `common/src/lib/organisation/organisation.api.ts`, `common/src/lib/admin/branding.api.ts`
- Frontend UI: `features/organisation/*`, `features/admin/platform-settings.component.*`, shell branding

## Implementation

- Add Branches screen with create/edit/deactivate.
- Add Teams screen with create/edit/deactivate and department/branch assignment.
- Add organization tree query or compose from branch/department/team APIs.
- Fix branding field bindings.
- Store logo as durable URL/asset.
- Add global default language setting.

## Code Example

```ts
export interface TeamRequest {
  readonly name: string;
  readonly branchId?: string | null;
  readonly departmentId?: string | null;
  readonly leadId?: string | null;
}
```

```html
<input type="color" formControlName="accentColor" />
<input type="url" formControlName="logoUrl" />
```

## Acceptance

- [ ] Admin can manage branches.
- [ ] Admin can manage teams.
- [ ] Organization tree shows branch, department, team hierarchy.
- [ ] Branding saves and applies to admin/portal runtime.
- [ ] Logo upload stores durable asset URL.
- [ ] Default language persists globally.

## Evidence

Pending.
