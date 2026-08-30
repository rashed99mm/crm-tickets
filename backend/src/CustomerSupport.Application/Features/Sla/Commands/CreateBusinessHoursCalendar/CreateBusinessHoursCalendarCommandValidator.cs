using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Sla.Commands.CreateBusinessHoursCalendar;

/// <summary>US-215, AC-228 — validates the shape of a calendar-row create before the handler runs.
/// Parsing lives here (not in the controller) so the field-keyed 400 comes from the shared
/// validation behaviour, matching every other command in the corpus.</summary>
public class CreateBusinessHoursCalendarCommandValidator
    : AbstractValidator<CreateBusinessHoursCalendarCommand>
{
    public CreateBusinessHoursCalendarCommandValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.REQUIRED_FIELD);

        RuleFor(x => x.DayOfWeek)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.REQUIRED_FIELD)
            .Must(s => Enum.TryParse<DayOfWeek>(s, ignoreCase: true, out _))
            .WithErrorCode(ApplicationErrors.Validation.INVALID_FORMAT);

        RuleFor(x => x.OpenTime)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.REQUIRED_FIELD)
            .Must(s => TimeOnly.TryParse(s, out _))
            .WithErrorCode(ApplicationErrors.Validation.INVALID_FORMAT);

        RuleFor(x => x.CloseTime)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.REQUIRED_FIELD)
            .Must(s => TimeOnly.TryParse(s, out _))
            .WithErrorCode(ApplicationErrors.Validation.INVALID_FORMAT);

        RuleFor(x => x)
            .Must(BeAfterOpen)
            .WithErrorCode(ApplicationErrors.Validation.INVALID_FORMAT);
    }

    private static bool BeAfterOpen(CreateBusinessHoursCalendarCommand command)
    {
        if (!TimeOnly.TryParse(command.OpenTime, out var open)
            || !TimeOnly.TryParse(command.CloseTime, out var close))
        {
            return true; // the individual rules above already report the unparseable field
        }

        return close > open;
    }
}
