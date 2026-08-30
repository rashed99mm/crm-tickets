using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Customers.Queries.GetCustomers;

/// <summary>
/// AC-11. An unbounded <c>pageSize</c> is a denial-of-service vector as much as a correctness
/// problem: one request asking for every row is all it takes.
/// </summary>
public class GetCustomersQueryValidator : AbstractValidator<GetCustomersQuery>
{
    public const int MaxPageSize = 100;

    public GetCustomersQueryValidator()
    {
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithErrorCode(ApplicationErrors.Validation.PAGE_SIZE_EXCEEDED);
    }
}
