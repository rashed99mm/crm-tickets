using CustomerSupport.Application.Errors;
using CustomerSupport.Domain.ValueObjects;
using FluentValidation;

namespace CustomerSupport.Application.Features.Tickets.Commands.ChangeTicketStatus;

/// <summary>
/// The shape rules for a status change.
///
/// The distinction this validator draws is the one AC-38 depends on: <c>Escalated</c> is a
/// <b>400</b> because there is no such status, while <c>Closed</c> from <c>New</c> is a
/// <b>409</b> because the status exists and the state is wrong. Both arrive at the same endpoint
/// and they must not answer alike — so "is this a real status" is validation, and "may this ticket
/// go there" is the handler's.
/// </summary>
public class ChangeTicketStatusCommandValidator : AbstractValidator<ChangeTicketStatusCommand>
{
    public ChangeTicketStatusCommandValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.STATUS_REQUIRED_FIELD)
            .Must(status => TicketStatus.TryCreate(status, out _, out _))
            .WithErrorCode(ApplicationErrors.Validation.TICKET_STATUS_INVALID);

        RuleFor(x => x.RowVersion)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.ROW_VERSION_REQUIRED)
            .Must(BeBase64).WithErrorCode(ApplicationErrors.Validation.ROW_VERSION_REQUIRED);

        // US-922 / AC-922.1: resolution is part of the request's *shape* when the target is
        // Resolved — absent fields are a 400 the form can key to controls, before any state check.
        When(x => IsResolvedTarget(x.Status), () =>
        {
            RuleFor(x => x.ResolutionCode)
                .NotEmpty().WithErrorCode(ApplicationErrors.Validation.RESOLUTION_CODE_REQUIRED)
                .Must(code => TicketResolutionCode.TryCreate(code, out _, out _))
                .WithErrorCode(ApplicationErrors.Validation.RESOLUTION_CODE_INVALID);

            RuleFor(x => x.ResolutionNotes)
                .NotEmpty().WithErrorCode(ApplicationErrors.Validation.RESOLUTION_NOTES_REQUIRED)
                .MaximumLength(2000).WithErrorCode(ApplicationErrors.Validation.RESOLUTION_NOTES_MAX_LENGTH);
        });
    }

    private static bool IsResolvedTarget(string? status) =>
        string.Equals(status?.Trim(), "Resolved", StringComparison.Ordinal);

    /// <summary>
    /// Checked here rather than left to <c>Convert.FromBase64String</c> in the handler, where a
    /// malformed value would surface as an unhandled <c>FormatException</c> and a 500 — which is
    /// AC-52's problem as much as this criterion's.
    /// </summary>
    internal static bool BeBase64(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Convert.TryFromBase64String(value, new byte[value.Length], out _);
}
