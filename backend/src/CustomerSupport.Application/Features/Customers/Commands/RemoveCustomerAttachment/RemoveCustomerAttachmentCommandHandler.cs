using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Assets;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Customers.Commands.RemoveCustomerAttachment;

/// <summary>Removes an attachment — the row and the file, AC-28.</summary>
public class RemoveCustomerAttachmentCommandHandler(
    IRepository<CustomerAttachment> attachments,
    IRepository<Asset> assets,
    IFileStore fileStore,
    IUnitOfWork unitOfWork,
    IMessageFactory messages,
    ILogger<RemoveCustomerAttachmentCommandHandler> logger)
    : ICommandHandler<RemoveCustomerAttachmentCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(
        RemoveCustomerAttachmentCommand request,
        CancellationToken ct)
    {
        var link = await attachments.FirstOrDefaultAsync(
            a => a.Id == request.AttachmentId && a.CustomerId == request.CustomerId, ct);

        if (link is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Attachment.NOT_FOUND);
        }

        var asset = await assets.GetByIdAsync(link.AssetId, ct);

        attachments.Remove(link);
        if (asset is not null)
        {
            assets.Remove(asset);
        }

        await unitOfWork.SaveChangesAsync(ct);

        if (asset is not null)
        {
            try
            {
                await fileStore.DeleteAsync(asset.StoredFileName, ct);
            }
            catch (IOException failure)
            {
                logger.LogError(
                    failure,
                    "Attachment {AttachmentId} was removed but its file {StoredFileName} could not be deleted",
                    link.Id,
                    asset.StoredFileName);
            }
        }

        return messages.Success(link.Id, ApplicationErrors.Attachment.REMOVED);
    }
}
