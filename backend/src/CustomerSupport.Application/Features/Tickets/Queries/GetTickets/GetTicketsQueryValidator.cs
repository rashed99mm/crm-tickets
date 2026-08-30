using CustomerSupport.Application.Errors;
using CustomerSupport.Domain.ValueObjects;
using FluentValidation;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetTickets;

/// <summary>
/// AC-33 and AC-11 for the queue.
///
/// An unknown status or priority is REFUSED rather than matched against nothing. The alternative
/// failure mode is silent: a typo'd filter returning an empty page reads to the user as "no tickets
/// in that state", which is indistinguishable from the truth and impossible to debug from the UI.
/// </summary>
public class GetTicketsQueryValidator : AbstractValidator<GetTicketsQuery>
{
    public const int MaxPageSize = 100;

    public GetTicketsQueryValidator()
    {
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithErrorCode(ApplicationErrors.Validation.PAGE_SIZE_EXCEEDED);

        RuleFor(x => x.Status)
            .Must(status => TicketStatus.TryCreate(status, out _, out _))
            .WithErrorCode(ApplicationErrors.Validation.TICKET_STATUS_INVALID)
            .When(x => !string.IsNullOrWhiteSpace(x.Status));

        RuleFor(x => x.Priority)
            .Must(priority => TicketPriority.TryCreate(priority, out _, out _))
            .WithErrorCode(ApplicationErrors.Validation.TICKET_PRIORITY_INVALID)
            .When(x => !string.IsNullOrWhiteSpace(x.Priority));
    }
}
