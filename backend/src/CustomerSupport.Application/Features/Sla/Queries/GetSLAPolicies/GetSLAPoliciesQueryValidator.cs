using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Sla.Queries.GetSLAPolicies;

/// <summary>AC-11's rule, applied here like every other paged surface.</summary>
public class GetSLAPoliciesQueryValidator : AbstractValidator<GetSLAPoliciesQuery>
{
    public GetSLAPoliciesQueryValidator()
    {
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithErrorCode(ApplicationErrors.Validation.PAGE_SIZE_EXCEEDED);
    }
}
