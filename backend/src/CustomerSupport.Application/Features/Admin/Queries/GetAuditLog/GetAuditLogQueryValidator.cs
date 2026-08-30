using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Admin.Queries.GetAuditLog;

/// <summary>AC-11's rule, applied here like every other paged surface.</summary>
public class GetAuditLogQueryValidator : AbstractValidator<GetAuditLogQuery>
{
    public GetAuditLogQueryValidator()
    {
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithErrorCode(ApplicationErrors.Validation.PAGE_SIZE_EXCEEDED);
    }
}
