using CustomerSupport.Application.Contracts;

using MediatR;

namespace CustomerSupport.Application.Features.Users.Commands.DeactivateUser;

public record DeactivateUserCommand(Guid Id) : ICommand<Response<Unit>>;
