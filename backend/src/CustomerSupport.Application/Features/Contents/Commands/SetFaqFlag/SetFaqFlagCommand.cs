using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Contents.Commands.SetFaqFlag;

public record SetFaqFlagCommand(Guid Id, bool IsFaq) : ICommand<Response<Guid>>;
