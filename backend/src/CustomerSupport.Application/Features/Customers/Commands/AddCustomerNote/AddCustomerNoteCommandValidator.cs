using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Customers.Commands.AddCustomerNote;

/// <summary>
/// AC-75 — a note must actually say something.
///
/// The entity refuses an empty body too, and by throwing; this validator exists so the refusal
/// reaches the note box as a field-keyed 400 instead of a 500. Written against the property
/// directly: an invoked lambda has no member expression, so the field key — the whole point —
/// would be lost.
/// </summary>
public class AddCustomerNoteCommandValidator : AbstractValidator<AddCustomerNoteCommand>
{
    /// <summary>Matches the column and the entity's own guard. All three must agree.</summary>
    public const int MaxBodyLength = 4000;

    public AddCustomerNoteCommandValidator()
    {
        // NotEmpty rejects whitespace as well as absence, which is what the criterion means by
        // empty: a note of three spaces is the same empty record as a note of none.
        RuleFor(x => x.Body)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.NOTE_BODY_REQUIRED)
            .MaximumLength(MaxBodyLength).WithErrorCode(ApplicationErrors.Validation.NOTE_BODY_MAX_LENGTH);
    }
}
