using CustomerSupport.Application.Contracts;

using MediatR;

namespace CustomerSupport.Application.Features.Contents.Commands.DeleteContent;

public record DeleteContentCommand(Guid Id) : ICommand<Response<Unit>>;
