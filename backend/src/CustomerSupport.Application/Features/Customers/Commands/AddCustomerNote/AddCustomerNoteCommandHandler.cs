using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Customers.Commands.AddCustomerNote;

/// <summary>
/// Records an interaction against a customer — AC-75, AC-76.
///
/// <c>CustomerId</c> is bound from the route and <c>Body</c> from the payload. There is no third
/// argument, which is the whole of AC-76: the author is not something this command can carry.
/// </summary>
public class AddCustomerNoteCommandHandler(
    IRepository<CustomerNote> notes,
    IRepository<Customer> customers,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<AddCustomerNoteCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(AddCustomerNoteCommand request, CancellationToken ct)
    {
        if (!await customers.ExistsAsync(c => c.Id == request.CustomerId, ct))
        {
            return messages.NotFound<Guid>(ApplicationErrors.Customer.NOT_FOUND);
        }

        var note = CustomerNote.Create(request.CustomerId, request.Body, userContext.UserId);

        await notes.AddAsync(note, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(note.Id, ApplicationErrors.Customer.NOTE_ADDED);
    }
}
