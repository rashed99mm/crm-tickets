using CustomerSupport.Application.Errors;
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

        // The single source of the four values is the value object, so adding a priority means
        // editing one file rather than one file and every validator that happens to list them.
        RuleFor(x => x.Priority)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.TICKET_PRIORITY_REQUIRED)
            .Must(BeAKnownPriority).WithErrorCode(ApplicationErrors.Validation.TICKET_PRIORITY_INVALID);

        // PJ-5. A source is optional — staff pass none — but when present it must name a real
        // origin channel. An empty/whitespace source is treated as "none" (the handler leaves the
        // platform default), so only genuinely invalid spellings are rejected here.
        When(x => !string.IsNullOrWhiteSpace(x.Source), () =>
            RuleFor(x => x.Source)
                .Must(BeAKnownSource).WithErrorCode(ApplicationErrors.Validation.TICKET_SOURCE_INVALID));
    }

    private static readonly string[] AllowedSources =
        { "Portal", "WebForm", "WhatsApp", "SMS", "Email", "LiveChat" };

    private static bool BeAKnownPriority(string? priority) =>
        TicketPriority.TryCreate(priority, out _, out _);

    private static bool BeAKnownSource(string? source) =>
        source is not null && AllowedSources.Contains(source);
}
