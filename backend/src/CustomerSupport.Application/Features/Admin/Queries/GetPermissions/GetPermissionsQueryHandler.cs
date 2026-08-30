using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Application.Features.Admin.Dtos;

namespace CustomerSupport.Application.Features.Admin.Queries.GetPermissions;

public sealed class GetPermissionsQueryHandler(
    IPermissionAdministrationService permissions,
    IMessageFactory messages)
    : IQueryHandler<GetPermissionsQuery, Response<PermissionAdministrationDto>>
{
    public async Task<Response<PermissionAdministrationDto>> Handle(GetPermissionsQuery request, CancellationToken ct)
    {
        return messages.Success(await permissions.GetAsync(ct), ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
