using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Organisation.Commands.CreateBranch;

/// <summary>AC-121.</summary>
public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.ORG_NAME_REQUIRED)
            .MaximumLength(200).WithErrorCode(ApplicationErrors.Validation.ORG_NAME_MAX_LENGTH);

        RuleFor(x => x.Timezone)
            .MaximumLength(100).WithErrorCode(ApplicationErrors.Validation.ORG_TIMEZONE_MAX_LENGTH)
            .When(x => x.Timezone is not null);
    }
}
