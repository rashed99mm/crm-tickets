using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Notifications.Commands.CreateNotification;
using FluentValidation;

namespace CustomerSupport.Application.Features.Notifications.Validators;

public class CreateNotificationRequestValidator : AbstractValidator<CreateNotificationRequest>
{
    public CreateNotificationRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required").WithErrorCode(ApplicationErrors.Validation.USER_ID_REQUIRED);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required").WithErrorCode(ApplicationErrors.Validation.TITLE_REQUIRED)
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters").WithErrorCode(ApplicationErrors.Validation.TITLE_MAX_LENGTH);

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required").WithErrorCode(ApplicationErrors.Validation.MESSAGE_REQUIRED)
            .MaximumLength(2000).WithMessage("Message must not exceed 2000 characters").WithErrorCode(ApplicationErrors.Validation.MESSAGE_MAX_LENGTH);

        RuleFor(x => x.NotificationType)
            .NotEmpty().WithMessage("Notification type is required").WithErrorCode(ApplicationErrors.Validation.NOTIFICATION_TYPE_REQUIRED)
            .MaximumLength(50).WithMessage("Notification type must not exceed 50 characters").WithErrorCode(ApplicationErrors.Validation.NOTIFICATION_TYPE_MAX_LENGTH);

        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("Channel is required").WithErrorCode(ApplicationErrors.Validation.CHANNEL_REQUIRED)
            .Must(c => c == "InApp" || c == "Email" || c == "SMS" || c == "Push")
            .WithMessage("Channel must be InApp, Email, SMS, or Push").WithErrorCode(ApplicationErrors.Validation.CHANNEL_INVALID);
    }
}
