# Task 09 - Security Admin Profile Completion

**Status:** Ready  
**Closes gaps:** User edit, department column, audit export, profile timezone/job title/notifications/billing tabs.

## Files

- Backend domain: `ApplicationUser.cs`, notification preference/billing profile entities as needed
- Backend API: `UsersController.cs`, `AuthController.cs`, `AdminController.cs`
- Frontend API: `common/src/lib/auth/staff.api.ts`, `common/src/lib/admin/audit-log.api.ts`
- Frontend UI: `features/users/*`, `features/account/profile.component.*`, `features/admin/audit-log.component.*`

## Implementation

- Add admin update user endpoint.
- Add department assignment to user DTO.
- Add profile settings DTO for timezone/job title/notifications/billing.
- Add user row menu with edit/deactivate/assign department.
- Add profile tabs with real save.
- Add audit export for active filters.

## Code Example

```csharp
public sealed record UpdateUserAdminRequest(
    string FirstName,
    string LastName,
    Guid? DepartmentId,
    string? JobTitle,
    string? TimeZone,
    IReadOnlyList<string> Roles);
```

## Acceptance

- [ ] User edit dialog persists identity and department fields.
- [ ] Department column is populated from API.
- [ ] Audit export is wired.
- [ ] Profile notifications and billing tabs load real data.
- [ ] Authorization prevents non-admin user edits.

## Evidence

Pending.
