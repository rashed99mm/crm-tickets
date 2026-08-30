using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Organisation.Commands.UpdateTeam;

public class UpdateTeamCommandValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.ORG_NAME_REQUIRED);

        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.ORG_NAME_REQUIRED)
            .MaximumLength(200).WithErrorCode(ApplicationErrors.Validation.ORG_NAME_MAX_LENGTH);
    }
}
