using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Customers.Commands.AddCustomerAttachment;

public record AddCustomerAttachmentCommand(
    Guid CustomerId,
    string FileName,
    string ContentType,
    long DeclaredLength,
    Stream Content) : ICommand<Response<Guid>>;
