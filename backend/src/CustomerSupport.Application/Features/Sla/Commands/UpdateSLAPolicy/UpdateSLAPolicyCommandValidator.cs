using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Sla.Commands.UpdateSLAPolicy;

/// <summary>Same rules creation uses.</summary>
public class UpdateSLAPolicyCommandValidator : AbstractValidator<UpdateSLAPolicyCommand>
{
    private static readonly string[] AllowedPriorities = ["Low", "Normal", "High", "Urgent"];

    public UpdateSLAPolicyCommandValidator()
    {
        RuleFor(x => x.Priority)
            .Must(p => AllowedPriorities.Contains(p))
            .WithErrorCode(ApplicationErrors.Validation.SLA_PRIORITY_INVALID);

        RuleFor(x => x.ResponseTargetHours)
            .GreaterThan(0).WithErrorCode(ApplicationErrors.Validation.SLA_RESPONSE_TARGET_INVALID);

        RuleFor(x => x.ResolutionTargetHours)
            .GreaterThan(0).WithErrorCode(ApplicationErrors.Validation.SLA_RESOLUTION_TARGET_INVALID);
    }
}
