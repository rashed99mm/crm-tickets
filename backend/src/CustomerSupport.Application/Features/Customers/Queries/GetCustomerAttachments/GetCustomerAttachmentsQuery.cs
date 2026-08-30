using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Customers.Dtos;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Application.Features.Customers.Queries.GetCustomerAttachments;

/// <summary>A customer's stored files, newest first — AC-22, AC-83.</summary>
public class GetCustomerAttachmentsQuery : BasePagedQuery, IQuery<Response<PaginatedList<CustomerAttachmentDto>>>
{
    public Guid CustomerId { get; init; }
}
