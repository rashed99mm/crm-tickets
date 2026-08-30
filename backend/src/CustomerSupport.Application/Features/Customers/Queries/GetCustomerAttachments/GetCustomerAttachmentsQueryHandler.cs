using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Customers.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Assets;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Customers.Queries.GetCustomerAttachments;

public class GetCustomerAttachmentsQueryHandler(
    IRepository<CustomerAttachment> attachments,
    IRepository<Asset> assets,
    IRepository<Customer> customers,
    IIdentityUserService identityUsers,
    IMessageFactory messages)
    : IQueryHandler<GetCustomerAttachmentsQuery, Response<PaginatedList<CustomerAttachmentDto>>>
{
    public async Task<Response<PaginatedList<CustomerAttachmentDto>>> Handle(
        GetCustomerAttachmentsQuery request,
        CancellationToken ct)
    {
        if (!await customers.ExistsAsync(c => c.Id == request.CustomerId, ct))
        {
            return messages.NotFound<PaginatedList<CustomerAttachmentDto>>(ApplicationErrors.Customer.NOT_FOUND);
        }

        var pageIndex = Math.Max(request.PageIndex, 1);
        var pageSize = Math.Max(request.PageSize, 1);

        var allAttachments = await attachments.ListAsync(a => a.CustomerId == request.CustomerId, ct);
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

        var total = joined.Count;

        var rows = joined
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var uploaderIds = rows.Select(r => r.Asset!.UploadedById).Distinct().ToList();
        var uploaderNames = new Dictionary<Guid, string>();
        foreach (var uploaderId in uploaderIds)
        {
            var uploader = await identityUsers.FindByIdAsync(uploaderId, ct);
            uploaderNames[uploaderId] = uploader?.FullName ?? string.Empty;
        }

        var items = rows.Select(r => new CustomerAttachmentDto(
            r.Id,
            r.Asset!.OriginalFileName,
            r.Asset.ContentType,
            r.Asset.SizeBytes,
            r.Asset.UploadedById,
            uploaderNames.GetValueOrDefault(r.Asset.UploadedById, string.Empty),
            r.CreatedAt)).ToList();

        return Response<PaginatedList<CustomerAttachmentDto>>.Ok(
            PaginatedList<CustomerAttachmentDto>.Create(items, total, pageIndex, pageSize),
            SystemCodeMap.Resolve("SUCCESS_OPERATION"), "OK");
    }
}
