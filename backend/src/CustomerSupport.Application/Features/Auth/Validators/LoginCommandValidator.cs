using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Auth.Commands.Login;
using FluentValidation;

namespace CustomerSupport.Application.Features.Auth.Validators;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required").WithErrorCode(ApplicationErrors.Validation.EMAIL_REQUIRED)
            .EmailAddress().WithMessage("Invalid email format").WithErrorCode(ApplicationErrors.Validation.INVALID_EMAIL);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required").WithErrorCode(ApplicationErrors.Validation.PASSWORD_REQUIRED);
    }
}
