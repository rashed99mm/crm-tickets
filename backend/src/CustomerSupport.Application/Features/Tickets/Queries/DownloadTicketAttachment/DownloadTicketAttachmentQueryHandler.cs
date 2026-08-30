using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Customers.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Assets;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Queries.DownloadTicketAttachment;

/// <summary>
/// Streams one ticket attachment's bytes back — TA-6.
///
/// A handler rather than a static file path, exactly as the customer download: a static path is a
/// URL reachable without a session, and streaming keeps the session check on the only route that can
/// reach the bytes. Portal calls scoped by <c>customerId</c> (TA-5); staff calls omit it.
/// </summary>
public class DownloadTicketAttachmentQueryHandler(
    IRepository<TicketAttachment> attachments,
    IRepository<Asset> assets,
    IRepository<Ticket> tickets,
    IFileStore fileStore,
    IMessageFactory messages)
    : IQueryHandler<DownloadTicketAttachmentQuery, Response<AttachmentContentDto>>
{
    public async Task<Response<AttachmentContentDto>> Handle(
        DownloadTicketAttachmentQuery request,
        CancellationToken ct)
    {
        var link = await attachments.FirstOrDefaultAsync(
            a => a.Id == request.AttachmentId && a.TicketId == request.TicketId, ct);

        if (link is null)
        {
            return messages.NotFound<AttachmentContentDto>(ApplicationErrors.Attachment.NOT_FOUND);
        }

        // Ownership scoping for the portal (TA-5): a customer may only download from their own
        // ticket, so a guessed link id on somebody else's ticket is refused here, not at the store.
        if (request.CustomerId is { } owner)
        {
            var ticket = await tickets.GetByIdAsync(link.TicketId, ct);
            if (ticket is null || ticket.CustomerId != owner)
            {
                return messages.NotFound<AttachmentContentDto>(ApplicationErrors.Ticket.NOT_FOUND);
            }
        }

        var asset = await assets.GetByIdAsync(link.AssetId, ct);
        if (asset is null)
        {
            return messages.NotFound<AttachmentContentDto>(ApplicationErrors.Attachment.NOT_FOUND);
        }

        var stream = await fileStore.OpenAsync(asset.StoredFileName, ct);
        if (stream is null)
        {
            return messages.NotFound<AttachmentContentDto>(ApplicationErrors.Attachment.NOT_FOUND);
        }

        return messages.Success(
            new AttachmentContentDto(stream, asset.ContentType, asset.OriginalFileName),
            ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
