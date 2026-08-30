using CustomerSupport.Application.Contracts;
using MediatR;

namespace CustomerSupport.Application.Features.Admin.Commands.AssignPermission;

public sealed record AssignPermissionCommand(Guid RoleId, Guid PermissionId) : ICommand<Response<Unit>>;
