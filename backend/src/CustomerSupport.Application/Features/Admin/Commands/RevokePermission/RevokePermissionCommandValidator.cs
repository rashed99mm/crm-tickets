using FluentValidation;

namespace CustomerSupport.Application.Features.Admin.Commands.RevokePermission;

public sealed class RevokePermissionCommandValidator : AbstractValidator<RevokePermissionCommand>
{
    public RevokePermissionCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.PermissionId).NotEmpty();
    }
}
