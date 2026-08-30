using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Customers.Dtos;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Application.Features.Customers.Queries.GetCustomerNotes;

/// <summary>A customer's interaction history, newest first — AC-74.</summary>
public class GetCustomerNotesQuery : BasePagedQuery, IQuery<Response<PaginatedList<CustomerNoteDto>>>
{
    public Guid CustomerId { get; init; }
}
