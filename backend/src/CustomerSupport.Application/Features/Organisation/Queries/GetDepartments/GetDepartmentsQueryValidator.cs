using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Organisation.Queries.GetDepartments;

/// <summary>AC-11's rule, applied here like every other paged surface.</summary>
public class GetDepartmentsQueryValidator : AbstractValidator<GetDepartmentsQuery>
{
    public GetDepartmentsQueryValidator()
    {
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithErrorCode(ApplicationErrors.Validation.PAGE_SIZE_EXCEEDED);
    }
}
