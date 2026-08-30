using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Customers.Commands.CreateCustomer;

public class CreateCustomerCommandHandler(
    IRepository<Customer> customers,
    IUserContext userContext,
    IIdentityUserService identityUsers,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IMessageFactory messages)
    : ICommandHandler<CreateCustomerCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateCustomerCommand request, CancellationToken ct)
    {
        var normalisedEmail = request.Email.Trim().ToLowerInvariant();

        if (await customers.ExistsAsync(c => c.Email == normalisedEmail, ct))
        {
            return messages.Fail<Guid>(ApplicationErrors.Customer.EMAIL_EXISTS, MessageType.Conflict);
        }

        var customer = Customer.Create(request.Name, request.Email, request.Phone);

        var actor = await identityUsers.FindByIdAsync(userContext.UserId, ct);
        if (actor?.BranchId is { } branchId)
        {
            customer.AssignBranch(branchId);
        }

        await customers.AddAsync(customer, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsUniqueViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Customer.EMAIL_EXISTS, MessageType.Conflict);
        }

        return messages.Success(customer.Id, ApplicationErrors.Customer.CREATED);
    }
}
