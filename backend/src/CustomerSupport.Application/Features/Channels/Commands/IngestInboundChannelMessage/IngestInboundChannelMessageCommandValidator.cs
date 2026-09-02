using CustomerSupport.Application.Errors;
using CustomerSupport.Domain.Common;
using FluentValidation;

namespace CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;

public class IngestInboundChannelMessageCommandValidator : AbstractValidator<IngestInboundChannelMessageCommand>
{
    private static readonly string[] AllowedChannels = ChannelNames.Inbound;

    public IngestInboundChannelMessageCommandValidator()
    {
        RuleFor(x => x.Channel)
            .Must(c => AllowedChannels.Contains(c))
            .WithErrorCode(ApplicationErrors.Validation.MESSAGE_CHANNEL_INVALID);

        RuleFor(x => x.Body)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.MESSAGE_BODY_REQUIRED)
            .MaximumLength(4000).WithErrorCode(ApplicationErrors.Validation.MESSAGE_BODY_MAX_LENGTH);

        RuleFor(x => x.CustomerName)
            .MaximumLength(200).WithErrorCode(ApplicationErrors.Validation.NAME_MAX_LENGTH)
            .When(x => x.CustomerName is not null);

        // Ticket.Create throws past 200 characters (Ticket.cs:135-138). Refusing it here turns an
        // unhandled ArgumentException into a field-keyed 400. SUBJECT_MAX_LENGTH already exists and
        // already has bilingual messages, so no Resources.yaml change is needed.
        RuleFor(x => x.Subject)
            .MaximumLength(200).WithErrorCode(ApplicationErrors.Validation.SUBJECT_MAX_LENGTH)
            .When(x => x.Subject is not null);

        RuleFor(x => x.CustomerEmail)
            .EmailAddress().WithErrorCode(ApplicationErrors.Validation.INVALID_EMAIL)
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail));

        RuleFor(x => x.CustomerPhone)
            .Must((request, phone) => !string.IsNullOrWhiteSpace(request.CustomerEmail) || !string.IsNullOrWhiteSpace(phone))
            .WithMessage("An inbound message needs a phone or an email.")
            .WithErrorCode(ApplicationErrors.Validation.CHANNEL_CONTACT_REQUIRED);
    }
}