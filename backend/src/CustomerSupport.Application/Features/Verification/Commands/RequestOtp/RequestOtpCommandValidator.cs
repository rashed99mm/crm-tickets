using CustomerSupport.Domain.Entities.Verification;
using FluentValidation;

namespace CustomerSupport.Application.Features.Verification.Commands.RequestOtp;

/// <summary>
/// A malformed contact or an unsupported channel is rejected here, before any record is loaded or
/// any provider is contacted (OTP-1, OTP-2).
/// </summary>
public class RequestOtpCommandValidator : AbstractValidator<RequestOtpCommand>
{
    public RequestOtpCommandValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("A valid verification type is required");

        RuleFor(x => x.Contact)
            .NotEmpty().WithMessage("A contact is required")
            .MaximumLength(256).WithMessage("The contact is too long");

        When(x => x.Type == OtpVerificationType.Email, () =>
        {
            RuleFor(x => x.Contact).EmailAddress().WithMessage("A valid email address is required");
        });

        When(x => x.Type == OtpVerificationType.Phone, () =>
        {
            RuleFor(x => x.Contact).Length(7, 32).WithMessage("A valid phone number is required");
        });
    }
}