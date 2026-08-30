using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetCategories;

public class GetCategoriesQueryHandler(IRepository<Category> categories)
    : IQueryHandler<GetCategoriesQuery, Response<IReadOnlyList<CategoryDto>>>
{
    public async Task<Response<IReadOnlyList<CategoryDto>>> Handle(GetCategoriesQuery request, CancellationToken ct)
    {
        var items = await categories.ListProjectedAsync(
            c => c.IsActive,
            c => new CategoryDto(c.Id, c.Name),
            ct);

        return Response<IReadOnlyList<CategoryDto>>.Ok(items, SystemCodeMap.Resolve("SUCCESS_OPERATION"), "OK");
    }
}
