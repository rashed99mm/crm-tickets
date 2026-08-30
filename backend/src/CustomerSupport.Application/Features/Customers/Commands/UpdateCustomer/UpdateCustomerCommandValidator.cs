using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Customers.Commands.CreateCustomer;
using FluentValidation;

namespace CustomerSupport.Application.Features.Customers.Commands.UpdateCustomer;

/// <summary>AC-14 — the same rules as creation. The criterion says so explicitly.</summary>
public class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.NAME_REQUIRED)
            .MaximumLength(200).WithErrorCode(ApplicationErrors.Validation.NAME_MAX_LENGTH);

        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.EMAIL_REQUIRED)
            .MaximumLength(320).WithErrorCode(ApplicationErrors.Validation.EMAIL_MAX_LENGTH)
            .Matches(CreateCustomerCommandValidator.EmailPattern)
            .WithErrorCode(ApplicationErrors.Validation.INVALID_EMAIL);

        RuleFor(x => x.Phone)
            .MaximumLength(32).WithErrorCode(ApplicationErrors.Validation.PHONE_MAX_LENGTH)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}
