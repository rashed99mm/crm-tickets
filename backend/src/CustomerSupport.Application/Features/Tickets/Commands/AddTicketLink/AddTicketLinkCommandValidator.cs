using CustomerSupport.Application.Errors;
using CustomerSupport.Domain.ValueObjects;
using FluentValidation;

namespace CustomerSupport.Application.Features.Tickets.Commands.AddTicketLink;

public class AddTicketLinkCommandValidator : AbstractValidator<AddTicketLinkCommand>
{
    public AddTicketLinkCommandValidator()
    {
        RuleFor(x => x.LinkType)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.TICKET_LINK_TYPE_INVALID)
            .Must(v => TicketLinkType.TryCreate(v, out _, out _))
            .WithErrorCode(ApplicationErrors.Validation.TICKET_LINK_TYPE_INVALID);

        RuleFor(x => x.TargetReference)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.TICKET_LINK_TARGET_REQUIRED);
    }
}
