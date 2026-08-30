using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Contents.Commands.PublishContent;

public record PublishContentCommand(Guid Id) : ICommand<Response<Guid>>;
