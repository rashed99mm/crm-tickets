using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;

namespace CustomerSupport.Application.Features.Admin.Commands.AssignPermission;

public sealed class AssignPermissionCommandHandler(
    IPermissionAdministrationService permissions,
    IMessageFactory messages)
    : ICommandHandler<AssignPermissionCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(AssignPermissionCommand request, CancellationToken ct)
    {
        var result = await permissions.AssignAsync(request.RoleId, request.PermissionId, ct);
        return result switch
        {
            PermissionMutationResult.RoleNotFound => messages.NotFound<Unit>(ApplicationErrors.Permission.ROLE_NOT_FOUND),
            PermissionMutationResult.PermissionNotFound => messages.NotFound<Unit>(ApplicationErrors.Permission.NOT_FOUND),
            _ => messages.Success(Unit.Value, ApplicationErrors.Permission.ASSIGNED)
        };
    }
}
