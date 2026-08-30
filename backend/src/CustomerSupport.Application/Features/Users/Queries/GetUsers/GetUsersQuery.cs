using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Users.Dtos;
using CustomerSupport.Domain;

namespace CustomerSupport.Application.Features.Users.Queries.GetUsers;

public record GetUsersQuery : IQuery<Response<PaginatedList<UserListItemDto>>>
{
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; } = "asc";
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
    public string? Role { get; init; }
}
