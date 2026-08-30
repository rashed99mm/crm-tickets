using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Organisation.Commands.UpdateBranch;

/// <summary>AC-121 — the same rules creation uses.</summary>
public class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.ORG_NAME_REQUIRED)
            .MaximumLength(200).WithErrorCode(ApplicationErrors.Validation.ORG_NAME_MAX_LENGTH);

        RuleFor(x => x.Timezone)
            .MaximumLength(100).WithErrorCode(ApplicationErrors.Validation.ORG_TIMEZONE_MAX_LENGTH)
            .When(x => x.Timezone is not null);
    }
}
