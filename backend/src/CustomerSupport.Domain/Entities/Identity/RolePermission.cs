namespace CustomerSupport.Domain.Entities.Identity;

public sealed class RolePermission
{
    private RolePermission() { }

    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public ApplicationRole Role { get; private set; } = null!;
    public Permission Permission { get; private set; } = null!;

    public static RolePermission Create(Guid roleId, Guid permissionId) => new()
    {
        RoleId = roleId,
        PermissionId = permissionId
    };
}
