using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Tickets.Commands.ChangeTicketStatus;
using CustomerSupport.Domain.ValueObjects;
using FluentValidation;

namespace CustomerSupport.Application.Features.Tickets.Commands.ReclassifyTicket;

/// <summary>AC-923.1 — both matrix inputs, always, plus the concurrency token's shape.</summary>
public class ReclassifyTicketCommandValidator : AbstractValidator<ReclassifyTicketCommand>
{
    public ReclassifyTicketCommandValidator()
    {
        RuleFor(x => x.Impact)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.TICKET_IMPACT_REQUIRED)
            .Must(v => TicketImpact.TryCreate(v, out _, out _))
            .WithErrorCode(ApplicationErrors.Validation.TICKET_IMPACT_INVALID);

        RuleFor(x => x.Urgency)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.TICKET_URGENCY_REQUIRED)
            .Must(v => TicketUrgency.TryCreate(v, out _, out _))
            .WithErrorCode(ApplicationErrors.Validation.TICKET_URGENCY_INVALID);

        RuleFor(x => x.RowVersion)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.ROW_VERSION_REQUIRED)
            .Must(ChangeTicketStatusCommandValidator.BeBase64)
            .WithErrorCode(ApplicationErrors.Validation.ROW_VERSION_REQUIRED);
    }
}
