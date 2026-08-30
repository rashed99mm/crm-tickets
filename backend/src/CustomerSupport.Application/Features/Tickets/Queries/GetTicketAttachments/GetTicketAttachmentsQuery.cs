using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Tickets.Dtos;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetTicketAttachments;

public record GetTicketAttachmentsQuery(
    Guid TicketId,

    /// <summary>Portal uses this to scope to the signed-in customer's own ticket (TA-5). Staff omits it.</summary>
    Guid? CustomerId = null) : IQuery<Response<IReadOnlyList<TicketAttachmentDto>>>;
