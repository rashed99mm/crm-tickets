using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Customers.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Customers.Queries.GetCustomerById;

/// <summary>One customer — AC-12.</summary>
public class GetCustomerByIdQueryHandler(
    IRepository<Customer> customers,
    IIdentityUserService identityUsers,
    IUserContext userContext,
    IMessageFactory messages)
    : IQueryHandler<GetCustomerByIdQuery, Response<CustomerDto>>
{
    public async Task<Response<CustomerDto>> Handle(GetCustomerByIdQuery request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.Id, ct);

        var actor = await identityUsers.FindByIdAsync(userContext.UserId, ct);
        if (customer is not null && actor?.BranchId is { } branchId && customer.BranchId != branchId)
        {
            customer = null;
        }

        if (customer is null)
        {
            return messages.NotFound<CustomerDto>(ApplicationErrors.Customer.NOT_FOUND);
        }

        return messages.Success(
            new CustomerDto(customer.Id, customer.Name, customer.Email, customer.Phone, customer.CreatedAt),
            ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
