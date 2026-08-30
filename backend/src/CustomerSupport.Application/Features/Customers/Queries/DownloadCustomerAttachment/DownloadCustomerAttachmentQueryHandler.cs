using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Customers.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Assets;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Customers.Queries.DownloadCustomerAttachment;

/// <summary>
/// Streams one attachment's bytes back — AC-26.
///
/// A handler rather than a static file path, and that is the criterion rather than a preference: a
/// static path is a URL, and a URL is reachable without a session. Streaming keeps the session
/// check on the only route that can reach the bytes.
/// </summary>
public class DownloadCustomerAttachmentQueryHandler(
    IRepository<CustomerAttachment> attachments,
    IRepository<Asset> assets,
    IFileStore fileStore,
    IMessageFactory messages)
    : IQueryHandler<DownloadCustomerAttachmentQuery, Response<AttachmentContentDto>>
{
    public async Task<Response<AttachmentContentDto>> Handle(
        DownloadCustomerAttachmentQuery request,
        CancellationToken ct)
    {
        var link = await attachments.FirstOrDefaultAsync(
            a => a.Id == request.AttachmentId && a.CustomerId == request.CustomerId, ct);

        if (link is null)
        {
            return messages.NotFound<AttachmentContentDto>(ApplicationErrors.Attachment.NOT_FOUND);
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
