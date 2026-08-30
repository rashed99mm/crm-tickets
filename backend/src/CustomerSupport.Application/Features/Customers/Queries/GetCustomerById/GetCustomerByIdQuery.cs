using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Customers.Dtos;

namespace CustomerSupport.Application.Features.Customers.Queries.GetCustomerById;

public record GetCustomerByIdQuery(Guid Id) : IQuery<Response<CustomerDto>>;
