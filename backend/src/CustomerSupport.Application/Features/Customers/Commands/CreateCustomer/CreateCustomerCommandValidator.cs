using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Customers.Commands.CreateCustomer;

/// <summary>
/// AC-8 — the create payload's rules.
///
/// These duplicate invariants <see cref="Domain.Entities.Customers.Customer"/> already enforces, and
/// that is deliberate rather than redundant: the validator produces a field-keyed 400 a form can
/// bind to, while the entity guarantees the invariant for every other caller — seeders, future
/// handlers, tests. Neither substitutes for the other.
///
/// Written against the properties directly, not through a shared helper taking lambdas: FluentValidation
/// derives <c>ValidationFailure.PropertyName</c> from the member expression, and that name is what
/// keys <c>errors[]</c> and therefore what the form binds to. A helper that hid the expression would
/// cost the field key, which is the whole criterion.
/// </summary>
public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    /// <summary>
    /// Not RFC 5322. It rejects the shapes an agent actually mistypes — a missing local part, a
    /// missing dotted domain, an embedded space — and does not try to adjudicate the exotic
    /// addresses only a delivery attempt can settle. Kept identical to the entity's.
    /// </summary>
    internal const string EmailPattern = @"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$";

    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.NAME_REQUIRED)
            .MaximumLength(200).WithErrorCode(ApplicationErrors.Validation.NAME_MAX_LENGTH);

        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.EMAIL_REQUIRED)
            .MaximumLength(320).WithErrorCode(ApplicationErrors.Validation.EMAIL_MAX_LENGTH)
            .Matches(EmailPattern).WithErrorCode(ApplicationErrors.Validation.INVALID_EMAIL);

        RuleFor(x => x.Phone)
            .MaximumLength(32).WithErrorCode(ApplicationErrors.Validation.PHONE_MAX_LENGTH)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}
