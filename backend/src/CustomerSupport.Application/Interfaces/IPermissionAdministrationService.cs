using CustomerSupport.Application.Features.Admin.Dtos;

namespace CustomerSupport.Application.Interfaces;

public enum PermissionMutationResult
{
    Succeeded,
    AlreadyAssigned,
    RoleNotFound,
    PermissionNotFound,
    MappingNotFound,
    LastPermissionRequired
}

public interface IPermissionAdministrationService
{
    Task<PermissionAdministrationDto> GetAsync(CancellationToken ct = default);
    Task<PermissionMutationResult> AssignAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
    Task<PermissionMutationResult> RevokeAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
}
