using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Customers.Queries.GetCustomers;
using FluentValidation;

namespace CustomerSupport.Application.Features.Customers.Queries.GetCustomerAttachments;

/// <summary>AC-11, applied to the attachments read like every other paged surface.</summary>
public class GetCustomerAttachmentsQueryValidator : AbstractValidator<GetCustomerAttachmentsQuery>
{
    public GetCustomerAttachmentsQueryValidator()
    {
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, GetCustomersQueryValidator.MaxPageSize)
            .WithErrorCode(ApplicationErrors.Validation.PAGE_SIZE_EXCEEDED);
    }
}
