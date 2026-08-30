namespace CustomerSupport.Application.Interfaces;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, string permissionName, CancellationToken ct = default);
}
