using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Organisation.Commands.CreateDepartment;

/// <summary>AC-121.</summary>
public class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.ORG_NAME_REQUIRED)
            .MaximumLength(200).WithErrorCode(ApplicationErrors.Validation.ORG_NAME_MAX_LENGTH);
    }
}
