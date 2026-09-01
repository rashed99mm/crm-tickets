# Task 01 — Set-role-permissions contract: command, validator, message codes (US-806, AC-806.6)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Admin/Commands/SetRolePermissions/SetRolePermissionsCommand.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Admin/Commands/SetRolePermissions/SetRolePermissionsCommandValidator.cs`
- Modify: `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs:133-141` (the `Permission` class) and `:345` (end of the `Validation` class, after `TICKET_LINK_TYPE_INVALID`)
- Modify: `backend/src/CustomerSupport.Application/Messages/SystemCode.cs:247` (after `CON078`, the last line of the class)
- Modify: `backend/src/CustomerSupport.Application/Messages/SystemCodeMap.cs:90-95` (the permission block)
- Modify: `backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml:41-58` (the `PERMISSION_*` block)
- Test: `backend/tests/CustomerSupport.Tests/Unit/Features/Admin/PermissionAdministrationTests.cs` (modify — the file has two tests today)

**Interfaces:**
- Consumes: `ICommand<TResponse>` (`Application/Contracts/ICommand.cs`), `Response<T>`,
  FluentValidation's `AbstractValidator<T>` — used exactly as
  `AssignPermissionCommandValidator.cs:7-14` does, and `.WithErrorCode(...)` as
  `CreateTicketCommandValidator.cs:12-24` does. `ResponseValidationBehavior.cs:27-33` turns each
  failure into `FieldError(PropertyName, SystemCodeMap.Resolve(PropertyName),
  localized(ErrorCode))` — which is why the field name and the error code both need registering, or
  the response carries the generic `ERR005`.
- Produces (later tasks rely on these exact names):
  - `sealed record SetRolePermissionsCommand(Guid RoleId, IReadOnlyList<Guid>? PermissionIds, IReadOnlyList<Guid>? ExpectedPermissionIds) : ICommand<Response<Unit>>`
  - `sealed record SetRolePermissionsRequest(IReadOnlyList<Guid>? PermissionIds, IReadOnlyList<Guid>? ExpectedPermissionIds)` — the body shape the controller binds in Task 03
  - `sealed class SetRolePermissionsCommandValidator : AbstractValidator<SetRolePermissionsCommand>`
  - `ApplicationErrors.Permission.UPDATED`, `ApplicationErrors.Permission.STALE_SNAPSHOT`
  - `ApplicationErrors.Validation.PERMISSION_SET_INVALID`, `ApplicationErrors.Validation.PERMISSION_SNAPSHOT_REQUIRED`
  - `SystemCode.CON079`, `SystemCode.ERR087`, `SystemCode.VAL080`, `SystemCode.VAL081`

**Why the lists are nullable.** `AC-806.6` requires a **400 with a field error** when
`expectedPermissionIds` is absent. If the command's properties were non-nullable
`IReadOnlyList<Guid>`, a missing JSON property would bind to `null` anyway (records do not validate
themselves) and the first `.Count` in the handler would throw a `NullReferenceException` → 500. So
they are declared nullable, the validator's `NotNull()` produces the 400, and the handler
dereferences with `!` — safe because `ResponseValidationBehavior` runs before every handler and
short-circuits on failure (`ResponseValidationBehavior.cs:25`).

## Steps

- [ ] **Step 1: Branch**

```bash
cd /c/new/php/week1-2_assessment/assessment-sdd
git checkout -b feat/feat-34-permissions-workbench
```

- [ ] **Step 2: Write the failing validator tests**

Append to `backend/tests/CustomerSupport.Tests/Unit/Features/Admin/PermissionAdministrationTests.cs`
(keep the two existing tests; add the `using` for the new namespace at the top of the file):

```csharp
using CustomerSupport.Application.Features.Admin.Commands.SetRolePermissions;
```

```csharp
    private static SetRolePermissionsCommand Command(
        IReadOnlyList<Guid>? permissionIds = null,
        IReadOnlyList<Guid>? expected = null,
        Guid? roleId = null)
        => new(roleId ?? Guid.NewGuid(), permissionIds ?? [Guid.NewGuid()], expected ?? []);

    [Fact] // AC-806.6
    [Trait("AC", "806.6")]
    public void Set_NullPermissionIds_IsRefusedWithAFieldError()
    {
        var result = new SetRolePermissionsCommandValidator().Validate(Command(permissionIds: null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.PropertyName == "PermissionIds")
            .Which.ErrorCode.Should().Be(ApplicationErrors.Validation.PERMISSION_SET_INVALID);
    }

    [Fact] // AC-806.6
    [Trait("AC", "806.6")]
    public void Set_NullExpectedPermissionIds_IsRefusedWithAFieldError()
    {
        var result = new SetRolePermissionsCommandValidator().Validate(Command(expected: null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.PropertyName == "ExpectedPermissionIds")
            .Which.ErrorCode.Should().Be(ApplicationErrors.Validation.PERMISSION_SNAPSHOT_REQUIRED);
    }

    [Fact] // AC-806.6
    [Trait("AC", "806.6")]
    public void Set_DuplicatePermissionIds_IsRefused()
    {
        var duplicated = Guid.NewGuid();

        var result = new SetRolePermissionsCommandValidator()
            .Validate(Command(permissionIds: [duplicated, duplicated]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "PermissionIds");
    }

    [Fact] // AC-806.6
    [Trait("AC", "806.6")]
    public void Set_EmptyGuidInPermissionIds_IsRefused()
    {
        var result = new SetRolePermissionsCommandValidator()
            .Validate(Command(permissionIds: [Guid.Empty]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName.StartsWith("PermissionIds"));
    }

    [Fact] // AC-806.4, AC-806.6
    [Trait("AC", "806.6")]
    public void Set_EmptyRoleId_IsRefused()
    {
        var result = new SetRolePermissionsCommandValidator().Validate(Command(roleId: Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "RoleId");
    }

    [Fact] // AC-806.2 — an empty set is a legal *request*; refusing it is the service's job, not the validator's
    [Trait("AC", "806.2")]
    public void Set_EmptyPermissionIds_PassesValidation()
    {
        var result = new SetRolePermissionsCommandValidator().Validate(Command(permissionIds: []));

        result.IsValid.Should().BeTrue();
    }
```

The last test is the one that keeps the layers honest: an empty `permissionIds` is well-formed
input. Whether it is *allowed* depends on whether the role is built-in, which is database state, so
it is refused in `SetAsync` with a 409 (Task 02) and never by the validator with a 400.

- [ ] **Step 3: Run the tests to verify they fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~PermissionAdministrationTests"
```

Expected: compile failure — `SetRolePermissionsCommand` and `SetRolePermissionsCommandValidator` do
not exist, and `ApplicationErrors.Validation.PERMISSION_SET_INVALID` is undefined. A compile failure
*is* a failing test here; do not proceed until you have seen it.

- [ ] **Step 4: Add the message keys**

In `ApplicationErrors.cs`, inside `public static class Permission` (currently lines 133-141, after
`LAST_REQUIRED`):

```csharp
        public const string UPDATED = "PERMISSION_UPDATED";
        public const string STALE_SNAPSHOT = "PERMISSION_STALE_SNAPSHOT";
```

In the same file, at the end of `public static class Validation` (after
`TICKET_LINK_TYPE_INVALID`, line 345):

```csharp
        // FEAT-34 / AC-806.6 — the batch permission set's field-keyed refusals.
        public const string PERMISSION_SET_INVALID = "PERMISSION_SET_INVALID";
        public const string PERMISSION_SNAPSHOT_REQUIRED = "PERMISSION_SNAPSHOT_REQUIRED";
```

In `SystemCode.cs`, after `CON078` (line 247, the last const in the class):

```csharp
        // FEAT-34 — role permission workbench (AC-806.x). Last used before this feature:
        // CON078, ERR086, VAL079.
        public const string CON079 = "CON079"; // Role permission set updated (AC-806.1)
        public const string ERR087 = "ERR087"; // Role permission snapshot is stale (AC-806.5)
        public const string VAL080 = "VAL080"; // Permission set invalid (AC-806.6)
        public const string VAL081 = "VAL081"; // Expected permission snapshot required (AC-806.6)
```

In `SystemCodeMap.cs`, in the permission block (lines 90-95, after `["PERMISSION_LAST_REQUIRED"]`):

```csharp
        ["PERMISSION_UPDATED"] = SystemCode.CON079,
        ["PERMISSION_STALE_SNAPSHOT"] = SystemCode.ERR087,
        // Field keys — ResponseValidationBehavior resolves the FluentValidation PropertyName, so
        // these two map the *property names*, not the error codes (see the behavior at :31).
        ["PermissionIds"] = SystemCode.VAL080,
        ["ExpectedPermissionIds"] = SystemCode.VAL081,
```

In `Resources.yaml`, after the `PERMISSION_LAST_REQUIRED` block (lines 56-58):

```yaml
PERMISSION_UPDATED:
  ar: "تم تحديث صلاحيات الدور بنجاح."
  en: "The role's permissions were updated successfully."
PERMISSION_STALE_SNAPSHOT:
  ar: "تم تعديل صلاحيات هذا الدور من مستخدم آخر. أعد تحميل الصفحة ثم حاول مرة أخرى."
  en: "This role's permissions were changed by someone else. Reload and try again."
PERMISSION_SET_INVALID:
  ar: "قائمة الصلاحيات غير صالحة."
  en: "The permission list is not valid."
PERMISSION_SNAPSHOT_REQUIRED:
  ar: "قائمة الصلاحيات المتوقعة مطلوبة."
  en: "The expected permission list is required."
```

- [ ] **Step 5: Write the command**

Create `SetRolePermissionsCommand.cs`:

```csharp
using CustomerSupport.Application.Contracts;
using MediatR;

namespace CustomerSupport.Application.Features.Admin.Commands.SetRolePermissions;

/// <summary>
/// Replaces a role's permission set in one transaction (AC-806.1).
///
/// <paramref name="ExpectedPermissionIds"/> is the set the caller staged from. A mismatch against
/// what is stored is refused, never merged (AC-806.5, spec A6) — two administrators editing the
/// same role must not silently overwrite one another.
///
/// Both lists are nullable so an absent JSON property becomes a field-keyed 400 from the validator
/// rather than a NullReferenceException in the handler (AC-806.6).
/// </summary>
public sealed record SetRolePermissionsCommand(
    Guid RoleId,
    IReadOnlyList<Guid>? PermissionIds,
    IReadOnlyList<Guid>? ExpectedPermissionIds) : ICommand<Response<Unit>>;

/// <summary>
/// The request body. <c>RoleId</c> is absent by design — it comes from the route, so there is no
/// second copy that could disagree with it.
/// </summary>
public sealed record SetRolePermissionsRequest(
    IReadOnlyList<Guid>? PermissionIds,
    IReadOnlyList<Guid>? ExpectedPermissionIds);
```

- [ ] **Step 6: Write the validator**

Create `SetRolePermissionsCommandValidator.cs`:

```csharp
using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Admin.Commands.SetRolePermissions;

/// <summary>
/// AC-806.6 — shape only. Whether a *well-formed* set is allowed (the built-in-role floor, an
/// unknown permission id, a stale snapshot) depends on database state and is decided in
/// <c>IPermissionAdministrationService.SetAsync</c>, which is why an empty list passes here.
/// </summary>
public sealed class SetRolePermissionsCommandValidator : AbstractValidator<SetRolePermissionsCommand>
{
    public SetRolePermissionsCommandValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.PERMISSION_SET_INVALID);

        RuleFor(x => x.PermissionIds)
            .NotNull().WithErrorCode(ApplicationErrors.Validation.PERMISSION_SET_INVALID)
            .Must(ids => ids!.Distinct().Count() == ids!.Count)
                .WithErrorCode(ApplicationErrors.Validation.PERMISSION_SET_INVALID)
                .WithMessage("The permission list contains duplicates.")
                .When(x => x.PermissionIds is not null);

        RuleForEach(x => x.PermissionIds)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.PERMISSION_SET_INVALID)
            .When(x => x.PermissionIds is not null);

        RuleFor(x => x.ExpectedPermissionIds)
            .NotNull().WithErrorCode(ApplicationErrors.Validation.PERMISSION_SNAPSHOT_REQUIRED);
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~PermissionAdministrationTests"
```

Expected: PASS, 8 tests (the 2 pre-existing plus the 6 added). Paste the output into this file's
**Test evidence** section below. If `Set_EmptyGuidInPermissionIds_IsRefused` fails on the property
name, print `result.Errors.Select(e => e.PropertyName)` — `RuleForEach` names it
`PermissionIds[0]`, which is why that assertion uses `StartsWith`.

- [ ] **Step 8: Verify the build is clean**

```bash
cd backend && dotnet build CustomerSupport.slnx
```

Expected: `Build succeeded`, 0 warnings (warnings are errors here).

- [ ] **Step 9: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Admin/Commands/SetRolePermissions \
        backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCode.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCodeMap.cs \
        backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml \
        backend/tests/CustomerSupport.Tests/Unit/Features/Admin/PermissionAdministrationTests.cs
git commit -m "feat: add set-role-permissions command and validator (AC-806.6)"
```

## Criteria covered

`AC-806.6` in full. `AC-806.2` gains its "an empty set is valid input" half here; the refusal itself
lands in Task 02.

## Test evidence

*Not yet executed — this task has not been run. Fill in with pasted output when it is.*

## Deviations from the plan

*None yet.*
