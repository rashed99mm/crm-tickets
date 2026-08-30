using CustomerSupport.Application.Features.Verification.Dtos;
using FluentValidation;

namespace CustomerSupport.Application.Features.Verification.Commands.VerifyOtp;

/// <summary>
/// A malformed code is rejected here, before any record is loaded or compared. The safe failure it
/// produces is indistinguishable from a wrong-code failure (AC-440).
/// </summary>
public class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.VerificationId)
            .NotEqual(Guid.Empty).WithMessage("A verification id is required");

        RuleFor(x => x.Code)
            .Matches(@"^\d{6}$").WithMessage("The code must be exactly six digits");
    }
}
