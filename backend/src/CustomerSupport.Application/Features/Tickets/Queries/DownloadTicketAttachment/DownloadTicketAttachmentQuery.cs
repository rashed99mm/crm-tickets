using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Customers.Dtos;

namespace CustomerSupport.Application.Features.Tickets.Queries.DownloadTicketAttachment;

public record DownloadTicketAttachmentQuery(
    Guid TicketId,
    Guid AttachmentId,

    /// <summary>Portal uses this to scope to the signed-in customer's own ticket (TA-5). Staff omits it.</summary>
    Guid? CustomerId = null) : IQuery<Response<AttachmentContentDto>>;
