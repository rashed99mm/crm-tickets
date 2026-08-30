using CustomerSupport.Application.Contracts;

using MediatR;

namespace CustomerSupport.Application.Features.Users.Commands.ActivateUser;

public record ActivateUserCommand(Guid Id) : ICommand<Response<Unit>>;
