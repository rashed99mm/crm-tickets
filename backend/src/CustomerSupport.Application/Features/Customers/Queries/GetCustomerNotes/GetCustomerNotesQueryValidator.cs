using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Customers.Queries.GetCustomers;
using FluentValidation;

namespace CustomerSupport.Application.Features.Customers.Queries.GetCustomerNotes;

/// <summary>AC-11, applied to the notes read like every other paged surface.</summary>
public class GetCustomerNotesQueryValidator : AbstractValidator<GetCustomerNotesQuery>
{
    public GetCustomerNotesQueryValidator()
    {
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, GetCustomersQueryValidator.MaxPageSize)
            .WithErrorCode(ApplicationErrors.Validation.PAGE_SIZE_EXCEEDED);
    }
}
