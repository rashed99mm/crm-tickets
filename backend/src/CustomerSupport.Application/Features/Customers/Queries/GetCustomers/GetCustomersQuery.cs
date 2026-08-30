using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Customers.Dtos;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Application.Features.Customers.Queries.GetCustomers;

public class GetCustomersQuery : BasePagedQuery, IQuery<Response<PaginatedList<CustomerDto>>>
{
    public string? Search { get; init; }
}
