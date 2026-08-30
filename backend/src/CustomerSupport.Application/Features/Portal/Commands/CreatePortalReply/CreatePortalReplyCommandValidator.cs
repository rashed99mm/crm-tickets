using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Portal.Commands.CreatePortalReply;

public class CreatePortalReplyCommandValidator : AbstractValidator<CreatePortalReplyCommand>
{
    public CreatePortalReplyCommandValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.MESSAGE_BODY_REQUIRED)
            .MaximumLength(4000).WithErrorCode(ApplicationErrors.Validation.MESSAGE_BODY_MAX_LENGTH);
    }
}