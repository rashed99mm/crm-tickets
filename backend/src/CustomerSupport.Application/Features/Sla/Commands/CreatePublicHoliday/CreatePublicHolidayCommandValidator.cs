using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Sla.Commands.CreatePublicHoliday;

/// <summary>US-215, AC-228 — validates the shape of a public-holiday create before the handler
/// runs. Parsing lives here (not the controller) so field-keyed 400s come from the shared
/// validation behaviour, matching every other command in the corpus.</summary>
public class CreatePublicHolidayCommandValidator : AbstractValidator<CreatePublicHolidayCommand>
{
    public CreatePublicHolidayCommandValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.REQUIRED_FIELD);

        RuleFor(x => x.HolidayDate)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.REQUIRED_FIELD)
            .Must(s => DateOnly.TryParse(s, out _))
            .WithErrorCode(ApplicationErrors.Validation.INVALID_FORMAT);

        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.REQUIRED_FIELD)
            .MaximumLength(200).WithErrorCode(ApplicationErrors.Validation.INVALID_FORMAT);
    }
}
