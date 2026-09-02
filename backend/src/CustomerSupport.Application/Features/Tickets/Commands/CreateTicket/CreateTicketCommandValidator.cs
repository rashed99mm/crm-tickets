using CustomerSupport.Application.Errors;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.ValueObjects;
using FluentValidation;

namespace CustomerSupport.Application.Features.Tickets.Commands.CreateTicket;

/// <summary>AC-30 — the create payload's rules, keyed by field.</summary>
public class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.SUBJECT_REQUIRED)
            .MaximumLength(200).WithErrorCode(ApplicationErrors.Validation.SUBJECT_MAX_LENGTH);

        RuleFor(x => x.Description)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.DESCRIPTION_REQUIRED);

        RuleFor(x => x.CustomerId)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.CUSTOMER_ID_REQUIRED);

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.CATEGORY_ID_REQUIRED);

        // US-923 / AC-923.1. Required from the staff surface (Source == null); customer-origin
        // callers omit both and the handler defaults them (spec A2). Whenever present, they must
        // be real matrix values regardless of origin.
        When(x => x.Source is null, () =>
        {
            RuleFor(x => x.Impact)
                .NotEmpty().WithErrorCode(ApplicationErrors.Validation.TICKET_IMPACT_REQUIRED);
            RuleFor(x => x.Urgency)
                .NotEmpty().WithErrorCode(ApplicationErrors.Validation.TICKET_URGENCY_REQUIRED);
        });

        When(x => !string.IsNullOrWhiteSpace(x.Impact), () =>
            RuleFor(x => x.Impact)
                .Must(v => TicketImpact.TryCreate(v, out _, out _))
                .WithErrorCode(ApplicationErrors.Validation.TICKET_IMPACT_INVALID));

        When(x => !string.IsNullOrWhiteSpace(x.Urgency), () =>
            RuleFor(x => x.Urgency)
                .Must(v => TicketUrgency.TryCreate(v, out _, out _))
                .WithErrorCode(ApplicationErrors.Validation.TICKET_URGENCY_INVALID));

        // PJ-5. A source is optional — staff pass none — but when present it must name a real
        // origin channel. An empty/whitespace source is treated as "none" (the handler leaves the
        // platform default), so only genuinely invalid spellings are rejected here.
        When(x => !string.IsNullOrWhiteSpace(x.Source), () =>
            RuleFor(x => x.Source)
                .Must(BeAKnownSource).WithErrorCode(ApplicationErrors.Validation.TICKET_SOURCE_INVALID));
    }

    private static readonly string[] AllowedSources = ChannelNames.TicketSources;

    private static bool BeAKnownSource(string? source) =>
        source is not null && AllowedSources.Contains(source);
}
