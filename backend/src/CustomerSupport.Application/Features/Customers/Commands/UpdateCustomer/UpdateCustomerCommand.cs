using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Customers.Commands.UpdateCustomer;

/// <summary>Corrects a customer record — AC-14.</summary>
public record UpdateCustomerCommand(Guid Id, string Name, string Email, string? Phone)
    : ICommand<Response<Guid>>;

/// <summary>
/// The update payload (AC-14). Same fields and the same rules as creation — the criterion says
/// "validation matches AC-8" rather than defining a second, laxer set.
/// </summary>
public record UpdateCustomerRequest(string Name, string Email, string? Phone);
