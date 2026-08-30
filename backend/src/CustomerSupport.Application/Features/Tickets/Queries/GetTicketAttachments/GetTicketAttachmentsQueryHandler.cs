using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Tickets.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Entities.Assets;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetTicketAttachments;

public class GetTicketAttachmentsQueryHandler(
    IRepository<TicketAttachment> attachments,
    IRepository<Asset> assets,
    IRepository<Ticket> tickets,
    IIdentityUserService identityUsers,
    IMessageFactory messages)
    : IQueryHandler<GetTicketAttachmentsQuery, Response<IReadOnlyList<TicketAttachmentDto>>>
{
    public async Task<Response<IReadOnlyList<TicketAttachmentDto>>> Handle(
        GetTicketAttachmentsQuery request,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(request.TicketId, ct);
        if (ticket is null
            || (request.CustomerId is { } owner && ticket.CustomerId != owner))
        {
            return messages.NotFound<IReadOnlyList<TicketAttachmentDto>>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var allAttachments = await attachments.ListAsync(a => a.TicketId == request.TicketId, ct);
        var assetIds = allAttachments.Select(a => a.AssetId).Distinct().ToList();
        var assetList = await assets.ListAsync(a => assetIds.Contains(a.Id), ct);
        var assetMap = assetList.ToDictionary(a => a.Id);

        var joined = allAttachments
            .Select(a => new
            {
                a.Id,
                Asset = assetMap.TryGetValue(a.AssetId, out var asset) ? asset : null,
                a.CreatedAt,
            })
            .Where(x => x.Asset != null)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        var uploaderIds = joined.Select(x => x.Asset!.UploadedById).Distinct().ToList();
        var uploaderNames = new Dictionary<Guid, string>();
        foreach (var uploaderId in uploaderIds)
        {
            var uploader = await identityUsers.FindByIdAsync(uploaderId, ct);
            uploaderNames[uploaderId] = uploader?.FullName ?? string.Empty;
        }

        var items = joined.Select(x => new TicketAttachmentDto(
            x.Id,
            x.Asset!.OriginalFileName,
            x.Asset.ContentType,
            x.Asset.SizeBytes,
            x.Asset.UploadedById,
            uploaderNames.GetValueOrDefault(x.Asset.UploadedById, string.Empty),
            x.CreatedAt)).ToList();

        return Response<IReadOnlyList<TicketAttachmentDto>>.Ok(
            items, SystemCodeMap.Resolve("SUCCESS_OPERATION"), "OK");
    }
}
