using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Assets;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Tickets.Commands.AddTicketAttachment;

/// <summary>
/// Stores a file against a ticket — TA-3, TA-4, TA-9.
///
/// Mirrors <c>AddCustomerAttachmentCommandHandler</c> exactly for size/type refusal, stream handling
/// and orphan cleanup, with one addition: an optional <c>customerId</c> that, when present (the
/// portal host), constrains the ticket to belong to that customer (TA-5). The declared length is
/// carried separately from the stream for the same reason as the customer handler — AC-23 has to be
/// answered before the stream is consumed.
/// </summary>
public class AddTicketAttachmentCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<Asset> assets,
    IRepository<TicketAttachment> attachments,
    IFileStore fileStore,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    IMessageFactory messages,
    FileStorageOptions options,
    ILogger<AddTicketAttachmentCommandHandler> logger)
    : ICommandHandler<AddTicketAttachmentCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(
        AddTicketAttachmentCommand request,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(request.TicketId, ct);
        if (ticket is null
            || (request.CustomerId is { } owner && ticket.CustomerId != owner))
        {
            // A ticket the requester may not see is "not found", never "forbidden": revealing the
            // multiplier is a cross-customer enumeration oracle (TA-5).
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        if (request.DeclaredLength <= 0)
        {
            return messages.Fail<Guid>(
                ApplicationErrors.Attachment.EMPTY,
                MessageType.Validation,
                [new FieldError("File", SystemCodeMap.Resolve(ApplicationErrors.Attachment.EMPTY), ApplicationErrors.Attachment.EMPTY)]);
        }

        if (request.DeclaredLength > options.MaxBytes)
        {
            return messages.Fail<Guid>(ApplicationErrors.Attachment.TOO_LARGE, MessageType.PayloadTooLarge);
        }

        var contentType = (request.ContentType ?? string.Empty).Split(';')[0].Trim();
        if (!options.AllowedContentTypes.Contains(contentType))
        {
            return messages.Fail<Guid>(ApplicationErrors.Attachment.TYPE_NOT_ALLOWED, MessageType.UnsupportedMediaType);
        }

        var asset = Asset.Create(
            request.FileName, contentType, request.DeclaredLength, userContext.UserId);

        var link = TicketAttachment.Create(request.TicketId, asset.Id);

        await fileStore.SaveAsync(asset.StoredFileName, request.Content, ct);

        try
        {
            await assets.AddAsync(asset, ct);
            await attachments.AddAsync(link, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            try
            {
                await fileStore.DeleteAsync(asset.StoredFileName, CancellationToken.None);
            }
            catch (Exception cleanupFailure)
            {
                logger.LogError(
                    cleanupFailure,
                    "Failed to remove orphaned attachment file {StoredFileName} after a failed save",
                    asset.StoredFileName);
            }

            throw;
        }

        return messages.Success(link.Id, ApplicationErrors.Attachment.ADDED);
    }
}
