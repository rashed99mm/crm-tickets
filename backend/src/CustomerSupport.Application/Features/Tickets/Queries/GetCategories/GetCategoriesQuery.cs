using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetCategories;

public record CategoryDto(Guid Id, string Name);

public record GetCategoriesQuery : IQuery<Response<IReadOnlyList<CategoryDto>>>;
