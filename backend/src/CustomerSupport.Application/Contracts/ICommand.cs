using MediatR;

namespace CustomerSupport.Application.Contracts;

public interface ICommand<TResponse> : IRequest<TResponse> { }
public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse> { }
