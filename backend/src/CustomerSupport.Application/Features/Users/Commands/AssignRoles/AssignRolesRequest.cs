namespace CustomerSupport.Application.Features.Users.Commands.AssignRoles;

public record AssignRolesRequest(IReadOnlyList<string> Roles);
