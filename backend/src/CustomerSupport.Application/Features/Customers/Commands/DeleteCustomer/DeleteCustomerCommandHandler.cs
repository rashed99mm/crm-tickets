using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using MediatR;

namespace CustomerSupport.Application.Features.Customers.Commands.DeleteCustomer;

/// <summary>Removes a customer, unless they hold support history — AC-15, AC-16.</summary>
public class DeleteCustomerCommandHandler(
    IRepository<Customer> customers,
    IRepository<Ticket> tickets,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<DeleteCustomerCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(DeleteCustomerCommand request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.Id, ct);
        if (customer is null)
        {
            return messages.NotFound<Unit>(ApplicationErrors.Customer.NOT_FOUND);
        }

        if (await tickets.ExistsAsync(t => t.CustomerId == request.Id, ct))
        {
            return messages.Fail<Unit>(ApplicationErrors.Customer.HAS_TICKETS, MessageType.Conflict);
        }

        customers.Remove(customer);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(Unit.Value, ApplicationErrors.Customer.DELETED);
    }
}
