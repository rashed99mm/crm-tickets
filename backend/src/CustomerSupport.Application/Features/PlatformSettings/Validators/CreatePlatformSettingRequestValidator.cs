using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.PlatformSettings.Dtos;
using FluentValidation;

namespace CustomerSupport.Application.Features.PlatformSettings.Validators;

public class CreatePlatformSettingRequestValidator : AbstractValidator<CreatePlatformSettingRequest>
{
    public CreatePlatformSettingRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("Key is required").WithErrorCode(ApplicationErrors.Validation.KEY_REQUIRED)
            .MaximumLength(100).WithMessage("Key must not exceed 100 characters").WithErrorCode(ApplicationErrors.Validation.KEY_MAX_LENGTH)
            .Matches("^[a-zA-Z0-9_.-]+$").WithMessage("Key can only contain letters, numbers, dots, underscores, and hyphens").WithErrorCode(ApplicationErrors.Validation.INVALID_FORMAT);

        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("Value is required").WithErrorCode(ApplicationErrors.Validation.VALUE_REQUIRED);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters").WithErrorCode(ApplicationErrors.Validation.MAX_LENGTH)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required").WithErrorCode(ApplicationErrors.Validation.REQUIRED_FIELD)
            .MaximumLength(100).WithMessage("Category must not exceed 100 characters").WithErrorCode(ApplicationErrors.Validation.MAX_LENGTH);

        RuleFor(x => x.ValueType)
            .NotEmpty().WithMessage("Value type is required").WithErrorCode(ApplicationErrors.Validation.REQUIRED_FIELD)
            .Must(t => t == "String" || t == "Number" || t == "Boolean" || t == "Json")
            .WithMessage("Value type must be String, Number, Boolean, or Json").WithErrorCode(ApplicationErrors.Validation.INVALID_FORMAT);
    }
}
