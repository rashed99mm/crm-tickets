using CustomerSupport.Application.Contracts;
using MediatR;

namespace CustomerSupport.Application.Features.Admin.Commands.RevokePermission;

public sealed record RevokePermissionCommand(Guid RoleId, Guid PermissionId) : ICommand<Response<Unit>>;
