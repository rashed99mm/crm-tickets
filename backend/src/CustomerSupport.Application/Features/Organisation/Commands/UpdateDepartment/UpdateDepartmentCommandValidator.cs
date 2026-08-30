using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Organisation.Commands.UpdateDepartment;

/// <summary>AC-121 — the same rules creation uses.</summary>
public class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.ORG_NAME_REQUIRED)
            .MaximumLength(200).WithErrorCode(ApplicationErrors.Validation.ORG_NAME_MAX_LENGTH);
    }
}
