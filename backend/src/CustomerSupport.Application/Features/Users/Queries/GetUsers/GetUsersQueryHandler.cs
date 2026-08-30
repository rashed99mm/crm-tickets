using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Users.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;

namespace CustomerSupport.Application.Features.Users.Queries.GetUsers;

public class GetUsersQueryHandler(IIdentityUserService identityUserService)
    : IQueryHandler<GetUsersQuery, Response<PaginatedList<UserListItemDto>>>
{
    public async Task<Response<PaginatedList<UserListItemDto>>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        var result = await identityUserService.GetUsersAsync(
            request.PageIndex,
            request.PageSize,
            request.SortBy,
            request.SortDirection,
            request.Search,
            request.IsActive,
            request.Role,
            ct);

        return Response<PaginatedList<UserListItemDto>>.Ok(result, SystemCodeMap.Resolve("SUCCESS_OPERATION"), "OK");
    }
}
