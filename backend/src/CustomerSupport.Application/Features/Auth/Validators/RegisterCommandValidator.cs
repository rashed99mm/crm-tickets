using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Auth.Commands.Register;
using FluentValidation;

namespace CustomerSupport.Application.Features.Auth.Validators;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required").WithErrorCode(ApplicationErrors.Validation.EMAIL_REQUIRED)
            .EmailAddress().WithMessage("Invalid email format").WithErrorCode(ApplicationErrors.Validation.INVALID_EMAIL)
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters").WithErrorCode(ApplicationErrors.Validation.MAX_LENGTH);

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required").WithErrorCode(ApplicationErrors.Validation.USERNAME_REQUIRED)
            .MinimumLength(3).WithMessage("Username must be at least 3 characters").WithErrorCode(ApplicationErrors.Validation.MIN_LENGTH)
            .MaximumLength(50).WithMessage("Username must not exceed 50 characters").WithErrorCode(ApplicationErrors.Validation.MAX_LENGTH)
            .Matches("^[a-zA-Z0-9_]+$").WithMessage("Username can only contain letters, numbers, and underscores").WithErrorCode(ApplicationErrors.Validation.INVALID_FORMAT);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required").WithErrorCode(ApplicationErrors.Validation.PASSWORD_REQUIRED)
            .MinimumLength(8).WithMessage("Password must be at least 8 characters").WithErrorCode(ApplicationErrors.Validation.MIN_LENGTH)
            .MaximumLength(100).WithMessage("Password must not exceed 100 characters").WithErrorCode(ApplicationErrors.Validation.MAX_LENGTH)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter").WithErrorCode(ApplicationErrors.Validation.PASSWORD_UPPERCASE)
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter").WithErrorCode(ApplicationErrors.Validation.PASSWORD_LOWERCASE)
            .Matches("[0-9]").WithMessage("Password must contain at least one number").WithErrorCode(ApplicationErrors.Validation.PASSWORD_NUMBER);

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required").WithErrorCode(ApplicationErrors.Validation.FIRST_NAME_REQUIRED)
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters").WithErrorCode(ApplicationErrors.Validation.MAX_LENGTH);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required").WithErrorCode(ApplicationErrors.Validation.LAST_NAME_REQUIRED)
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters").WithErrorCode(ApplicationErrors.Validation.MAX_LENGTH);

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters").WithErrorCode(ApplicationErrors.Validation.MAX_LENGTH);
    }
}
