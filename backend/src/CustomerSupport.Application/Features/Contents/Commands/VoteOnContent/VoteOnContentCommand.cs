using CustomerSupport.Application.Contracts;
using MediatR;

namespace CustomerSupport.Application.Features.Contents.Commands.VoteOnContent;

public record VoteOnContentCommand(Guid ContentId, bool IsHelpful) : ICommand<Response<Unit>>;
