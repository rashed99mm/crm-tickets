using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Organisation.Queries.GetBranches;

/// <summary>AC-11's rule, applied here like every other paged surface.</summary>
public class GetBranchesQueryValidator : AbstractValidator<GetBranchesQuery>
{
    public GetBranchesQueryValidator()
    {
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithErrorCode(ApplicationErrors.Validation.PAGE_SIZE_EXCEEDED);
    }
}
