using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Customers.Commands.RemoveCustomerAttachment;

public record RemoveCustomerAttachmentCommand(Guid CustomerId, Guid AttachmentId)
    : ICommand<Response<Guid>>;
