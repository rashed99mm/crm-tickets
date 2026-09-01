using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;

namespace CustomerSupport.Application.Features.Admin.Commands.SetRolePermissions;

public sealed class SetRolePermissionsCommandHandler(
    IPermissionAdministrationService permissions,
    IMessageFactory messages)
    : ICommandHandler<SetRolePermissionsCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(SetRolePermissionsCommand request, CancellationToken ct)
    {
        // Both lists are non-null here: ResponseValidationBehavior short-circuits on the validator's
        // NotNull rules before any handler runs (ResponseValidationBehavior.cs:25).
        var result = await permissions.SetAsync(
            request.RoleId, request.PermissionIds!, request.ExpectedPermissionIds!, ct);

        return result switch
        {
            PermissionMutationResult.RoleNotFound =>
                messages.NotFound<Unit>(ApplicationErrors.Permission.ROLE_NOT_FOUND),
            PermissionMutationResult.PermissionNotFound =>
                messages.NotFound<Unit>(ApplicationErrors.Permission.NOT_FOUND),
            PermissionMutationResult.StaleSnapshot =>
                messages.Fail<Unit>(ApplicationErrors.Permission.STALE_SNAPSHOT, MessageType.Conflict),
            PermissionMutationResult.LastPermissionRequired =>
                messages.Fail<Unit>(ApplicationErrors.Permission.LAST_REQUIRED, MessageType.Conflict),
            _ => messages.Success(Unit.Value, ApplicationErrors.Permission.UPDATED)
        };
    }
}
