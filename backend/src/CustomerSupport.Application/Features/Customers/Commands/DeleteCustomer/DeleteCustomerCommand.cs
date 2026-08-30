using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Common;
using MediatR;

namespace CustomerSupport.Application.Features.Customers.Commands.DeleteCustomer;

public record DeleteCustomerCommand(Guid Id) : ICommand<Response<Unit>>;
