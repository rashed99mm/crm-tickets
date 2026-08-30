using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Customers.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Customers.Queries.GetCustomers;

/// <summary>The paged customer list — AC-10, AC-11, AC-13.</summary>
public class GetCustomersQueryHandler(
    IRepository<Customer> customers,
    IIdentityUserService identityUsers,
    IUserContext userContext)
    : IQueryHandler<GetCustomersQuery, Response<PaginatedList<CustomerDto>>>
{
    public async Task<Response<PaginatedList<CustomerDto>>> Handle(GetCustomersQuery request, CancellationToken ct)
    {
        var filter = PredicateBuilder.True<Customer>()
            .WhereIf(!string.IsNullOrWhiteSpace(request.Search),
                c => c.Name.Contains(request.Search!) || c.Email.Contains(request.Search!));

        var actor = await identityUsers.FindByIdAsync(userContext.UserId, ct);
        if (actor?.BranchId is { } branchId)
        {
            filter = filter.And(c => c.BranchId == branchId);
        }

        var page = await customers.GetPagedAsync(
            request,
            filter,
            c => new CustomerDto(c.Id, c.Name, c.Email, c.Phone, c.CreatedAt),
            ct);

        return Response<PaginatedList<CustomerDto>>.Ok(page, SystemCodeMap.Resolve("SUCCESS_OPERATION"), "OK");
    }
}
