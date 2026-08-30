using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Contents.Commands.AssignContentCategory;

public record AssignContentCategoryCommand(Guid ContentId, Guid? CategoryId) : ICommand<Response<Guid>>;
