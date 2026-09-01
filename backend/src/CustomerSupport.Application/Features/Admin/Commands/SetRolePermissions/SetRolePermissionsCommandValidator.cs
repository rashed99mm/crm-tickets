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

        // A trailing `.When(...)` in FluentValidation applies to every validator already chained
        // onto this RuleFor by default (ApplyConditionTo.AllValidators) — attaching the null-guard
        // after NotNull() would disable NotNull() itself whenever the value actually is null. Kept
        // as its own RuleFor so the two concerns cannot interact.
        RuleFor(x => x.PermissionIds)
            .NotNull().WithErrorCode(ApplicationErrors.Validation.PERMISSION_SET_INVALID);

        RuleFor(x => x.PermissionIds)
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
