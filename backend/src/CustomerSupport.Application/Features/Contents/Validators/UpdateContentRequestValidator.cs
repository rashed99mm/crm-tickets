using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Contents.Commands.UpdateContent;
using FluentValidation;

namespace CustomerSupport.Application.Features.Contents.Validators;

public class UpdateContentRequestValidator : AbstractValidator<UpdateContentRequest>
{
    public UpdateContentRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(500).WithMessage("Title must not exceed 500 characters").WithErrorCode(ApplicationErrors.Validation.TITLE_MAX_LENGTH)
            .When(x => !string.IsNullOrEmpty(x.Title));

        RuleFor(x => x.Summary)
            .MaximumLength(1000).WithMessage("Summary must not exceed 1000 characters").WithErrorCode(ApplicationErrors.Validation.SUMMARY_MAX_LENGTH)
            .When(x => !string.IsNullOrEmpty(x.Summary));

        RuleFor(x => x.Status)
            .Must(s => s == "Draft" || s == "Published" || s == "Archived")
            .WithMessage("Status must be Draft, Published, or Archived").WithErrorCode(ApplicationErrors.Validation.STATUS_INVALID)
            .When(x => !string.IsNullOrEmpty(x.Status));

        RuleFor(x => x.FeaturedImageUrl)
            .MaximumLength(2000).WithMessage("Featured image URL must not exceed 2000 characters").WithErrorCode(ApplicationErrors.Validation.FEATURED_IMAGE_URL_MAX_LENGTH)
            .When(x => !string.IsNullOrEmpty(x.FeaturedImageUrl));

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage("Category must not exceed 100 characters").WithErrorCode(ApplicationErrors.Validation.CATEGORY_MAX_LENGTH)
            .When(x => !string.IsNullOrEmpty(x.Category));
    }
}
