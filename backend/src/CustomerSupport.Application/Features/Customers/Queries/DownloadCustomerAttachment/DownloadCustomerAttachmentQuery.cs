using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Customers.Dtos;

namespace CustomerSupport.Application.Features.Customers.Queries.DownloadCustomerAttachment;

public record DownloadCustomerAttachmentQuery(Guid CustomerId, Guid AttachmentId)
    : IQuery<Response<AttachmentContentDto>>;
