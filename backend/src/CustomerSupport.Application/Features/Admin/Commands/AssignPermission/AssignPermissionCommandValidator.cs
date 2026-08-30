using FluentValidation;

namespace CustomerSupport.Application.Features.Admin.Commands.AssignPermission;

public sealed class AssignPermissionCommandValidator : AbstractValidator<AssignPermissionCommand>
{
    public AssignPermissionCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.PermissionId).NotEmpty();
    }
}
