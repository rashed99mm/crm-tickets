using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.PlatformSettings.Dtos;
using FluentValidation;

namespace CustomerSupport.Application.Features.PlatformSettings.Validators;

public class UpdatePlatformSettingRequestValidator : AbstractValidator<UpdatePlatformSettingRequest>
{
    public UpdatePlatformSettingRequestValidator()
    {
        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters").WithErrorCode(ApplicationErrors.Validation.MAX_LENGTH)
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
