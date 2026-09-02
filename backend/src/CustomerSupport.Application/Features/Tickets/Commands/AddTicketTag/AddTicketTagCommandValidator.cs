using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Tickets.Commands.AddTicketTag;

/// <summary>
/// Shape only: present and not absurdly long (2x the normalized cap, since collapsing may shrink
/// it). Charset/length precision lives in <c>TagValue.Normalize</c>, surfaced by the handler as
/// the same field-keyed 400.
/// </summary>
public class AddTicketTagCommandValidator : AbstractValidator<AddTicketTagCommand>
{
    public AddTicketTagCommandValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.TICKET_TAG_INVALID)
            .MaximumLength(60).WithErrorCode(ApplicationErrors.Validation.TICKET_TAG_INVALID);
    }
}
