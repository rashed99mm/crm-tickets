using CustomerSupport.Application.Errors;
using CustomerSupport.Domain.Entities.Survey;
using FluentValidation;

namespace CustomerSupport.Application.Features.Portal.Commands.SubmitSurvey;

public class SubmitSurveyCommandValidator : AbstractValidator<SubmitSurveyCommand>
{
    public SubmitSurveyCommandValidator()
    {
        RuleFor(x => x.Rating)
            .InclusiveBetween(SurveyResponse.MinRating, SurveyResponse.MaxRating)
            .WithErrorCode(ApplicationErrors.Survey.RATING_INVALID);

        RuleFor(x => x.Comment)
            .MaximumLength(SurveyResponse.MaxFreeTextLength)
            .WithErrorCode(ApplicationErrors.Validation.MAX_LENGTH)
            .When(x => !string.IsNullOrWhiteSpace(x.Comment));
    }
}