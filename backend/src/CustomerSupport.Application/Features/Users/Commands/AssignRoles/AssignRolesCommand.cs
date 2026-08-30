using CustomerSupport.Application.Contracts;

using MediatR;

namespace CustomerSupport.Application.Features.Users.Commands.AssignRoles;

public record AssignRolesCommand(
    Guid UserId,
    IReadOnlyList<string> Roles
) : ICommand<Response<Unit>>;
