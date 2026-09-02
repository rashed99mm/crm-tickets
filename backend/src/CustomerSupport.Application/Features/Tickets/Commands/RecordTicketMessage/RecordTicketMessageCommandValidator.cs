using CustomerSupport.Application.Errors;
using CustomerSupport.Domain.Common;
using FluentValidation;

namespace CustomerSupport.Application.Features.Tickets.Commands.RecordTicketMessage;

public class RecordTicketMessageCommandValidator : AbstractValidator<RecordTicketMessageCommand>
{
    private static readonly string[] AllowedDirections = ["Inbound", "Outbound"];
    private static readonly string[] AllowedChannels = ChannelNames.All;

    public RecordTicketMessageCommandValidator()
    {
        RuleFor(x => x.Direction)
            .Must(d => AllowedDirections.Contains(d))
            .WithErrorCode(ApplicationErrors.Validation.MESSAGE_DIRECTION_INVALID);

        RuleFor(x => x.Channel)
            .Must(c => AllowedChannels.Contains(c))
            .WithErrorCode(ApplicationErrors.Validation.MESSAGE_CHANNEL_INVALID);

        RuleFor(x => x.Subject)
            .MaximumLength(200).WithErrorCode(ApplicationErrors.Validation.MESSAGE_SUBJECT_MAX_LENGTH)
            .When(x => x.Subject is not null);

        RuleFor(x => x.Body)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.MESSAGE_BODY_REQUIRED)
            .MaximumLength(4000).WithErrorCode(ApplicationErrors.Validation.MESSAGE_BODY_MAX_LENGTH);
    }
}
