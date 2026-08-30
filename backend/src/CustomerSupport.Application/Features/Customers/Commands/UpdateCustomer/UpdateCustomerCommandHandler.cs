using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Customers.Commands.UpdateCustomer;

public class UpdateCustomerCommandHandler(
    IRepository<Customer> customers,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IMessageFactory messages)
    : ICommandHandler<UpdateCustomerCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(UpdateCustomerCommand request, CancellationToken ct)
    {
        var customer = await customers.GetTrackedAsync(request.Id, ct);
        if (customer is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Customer.NOT_FOUND);
        }

        customer.Update(request.Name, request.Email, request.Phone);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsUniqueViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Customer.EMAIL_EXISTS, MessageType.Conflict);
        }

        return messages.Success(customer.Id, ApplicationErrors.Customer.UPDATED);
    }
}
