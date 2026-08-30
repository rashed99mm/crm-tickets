using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.ContentCategories.Commands.CreateContentCategory;

public record CreateContentCategoryCommand(string Name, Guid? ParentId) : ICommand<Response<Guid>>;
