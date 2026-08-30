using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Customers.Commands.CreateCustomer;

/// <summary>Records a new customer — AC-7.</summary>
public record CreateCustomerCommand(string Name, string Email, string? Phone)
    : ICommand<Response<Guid>>;

/// <summary>The create payload (AC-7). Validated by <c>CreateCustomerCommandValidator</c>.</summary>
public record CreateCustomerRequest(string Name, string Email, string? Phone);
