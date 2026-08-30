using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Customers.Commands.AddCustomerNote;

public record AddCustomerNoteCommand(Guid CustomerId, string Body)
    : ICommand<Response<Guid>>;
