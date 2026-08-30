using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;

namespace CustomerSupport.Application.Features.Admin.Commands.RevokePermission;

public sealed class RevokePermissionCommandHandler(
    IPermissionAdministrationService permissions,
    IMessageFactory messages)
    : ICommandHandler<RevokePermissionCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(RevokePermissionCommand request, CancellationToken ct)
    {
        var result = await permissions.RevokeAsync(request.RoleId, request.PermissionId, ct);
        return result switch
        {
            PermissionMutationResult.RoleNotFound => messages.NotFound<Unit>(ApplicationErrors.Permission.ROLE_NOT_FOUND),
            PermissionMutationResult.PermissionNotFound => messages.NotFound<Unit>(ApplicationErrors.Permission.NOT_FOUND),
            PermissionMutationResult.MappingNotFound => messages.NotFound<Unit>(ApplicationErrors.Permission.MAPPING_NOT_FOUND),
            PermissionMutationResult.LastPermissionRequired => messages.Fail<Unit>(
                ApplicationErrors.Permission.LAST_REQUIRED, MessageType.Conflict),
            _ => messages.Success(Unit.Value, ApplicationErrors.Permission.REVOKED)
        };
    }
}
