using CustomerSupport.Application.Contracts;

using MediatR;

namespace CustomerSupport.Application.Features.Users.Commands.DeleteUser;

public record DeleteUserCommand(Guid Id) : ICommand<Response<Unit>>;
