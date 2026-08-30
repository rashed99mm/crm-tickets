using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Contents.Commands.ArchiveContent;

public record ArchiveContentCommand(Guid Id) : ICommand<Response<Guid>>;
