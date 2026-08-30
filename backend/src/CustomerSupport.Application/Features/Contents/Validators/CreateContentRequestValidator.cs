using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Contents.Commands.CreateContent;
using FluentValidation;

namespace CustomerSupport.Application.Features.Contents.Validators;

public class CreateContentRequestValidator : AbstractValidator<CreateContentRequest>
{
    public CreateContentRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required").WithErrorCode(ApplicationErrors.Validation.TITLE_REQUIRED)
            .MaximumLength(500).WithMessage("Title must not exceed 500 characters").WithErrorCode(ApplicationErrors.Validation.TITLE_MAX_LENGTH);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required").WithErrorCode(ApplicationErrors.Validation.BODY_REQUIRED);

        RuleFor(x => x.Summary)
            .MaximumLength(1000).WithMessage("Summary must not exceed 1000 characters").WithErrorCode(ApplicationErrors.Validation.SUMMARY_MAX_LENGTH)
            .When(x => !string.IsNullOrEmpty(x.Summary));

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required").WithErrorCode(ApplicationErrors.Validation.CONTENT_TYPE_REQUIRED)
            .MaximumLength(50).WithMessage("Content type must not exceed 50 characters").WithErrorCode(ApplicationErrors.Validation.CONTENT_TYPE_MAX_LENGTH);

        RuleFor(x => x.AuthorId)
            .NotEmpty().WithMessage("Author ID is required").WithErrorCode(ApplicationErrors.Validation.AUTHOR_ID_REQUIRED);

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required").WithErrorCode(ApplicationErrors.Validation.STATUS_REQUIRED)
            .Must(s => s == "Draft" || s == "Published" || s == "Archived")
            .WithMessage("Status must be Draft, Published, or Archived").WithErrorCode(ApplicationErrors.Validation.STATUS_INVALID);

        RuleFor(x => x.FeaturedImageUrl)
            .MaximumLength(2000).WithMessage("Featured image URL must not exceed 2000 characters").WithErrorCode(ApplicationErrors.Validation.FEATURED_IMAGE_URL_MAX_LENGTH)
            .When(x => !string.IsNullOrEmpty(x.FeaturedImageUrl));

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage("Category must not exceed 100 characters").WithErrorCode(ApplicationErrors.Validation.CATEGORY_MAX_LENGTH)
            .When(x => !string.IsNullOrEmpty(x.Category));
    }
}
