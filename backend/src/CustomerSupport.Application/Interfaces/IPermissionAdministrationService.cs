using CustomerSupport.Application.Features.Admin.Dtos;

namespace CustomerSupport.Application.Interfaces;

public enum PermissionMutationResult
{
    Succeeded,
    AlreadyAssigned,
    RoleNotFound,
    PermissionNotFound,
    MappingNotFound,
    LastPermissionRequired,
    StaleSnapshot
}

public interface IPermissionAdministrationService
{
    Task<PermissionAdministrationDto> GetAsync(CancellationToken ct = default);
    Task<PermissionMutationResult> AssignAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
    Task<PermissionMutationResult> RevokeAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);

    /// <summary>
    /// Replaces the role's permission set with <paramref name="permissionIds"/> in one transaction
    /// (AC-806.1). Refuses with <see cref="PermissionMutationResult.StaleSnapshot"/> when the stored
    /// set does not set-equal <paramref name="expectedPermissionIds"/> (AC-806.5), and with
    /// <see cref="PermissionMutationResult.LastPermissionRequired"/> when the request would leave a
    /// built-in role with nothing (AC-806.2). Either refusal writes nothing.
    /// </summary>
    Task<PermissionMutationResult> SetAsync(
        Guid roleId,
        IReadOnlyCollection<Guid> permissionIds,
        IReadOnlyCollection<Guid> expectedPermissionIds,
        CancellationToken ct = default);
}
