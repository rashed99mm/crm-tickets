using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.AddTicketAttachment;

public record AddTicketAttachmentCommand(
    Guid TicketId,
    string FileName,
    string ContentType,
    long DeclaredLength,
    Stream Content,

    /// <summary>
    /// Optional owner scoping for the portal host (TA-5). When provided, the ticket must belong to
    /// this customer or the upload is refused as not-found. Staff passes null — any authenticated
    /// user with ticket access may attach.
    /// </summary>
    Guid? CustomerId = null) : ICommand<Response<Guid>>;
