using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Tickets.Commands.ChangeTicketStatus;
using FluentValidation;

namespace CustomerSupport.Application.Features.Tickets.Commands.TakeEscalation;

public class TakeEscalationCommandValidator : AbstractValidator<TakeEscalationCommand>
{
    public TakeEscalationCommandValidator()
    {
        RuleFor(x => x.AssigneeId)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.ASSIGNEE_ID_REQUIRED);

        RuleFor(x => x.RowVersion)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.ROW_VERSION_REQUIRED)
            .Must(ChangeTicketStatusCommandValidator.BeBase64)
            .WithErrorCode(ApplicationErrors.Validation.ROW_VERSION_REQUIRED);
    }
}
