using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Security;

public sealed class PermissionService(AppDbContext db) : IPermissionService
{
    public async Task<bool> HasPermissionAsync(Guid userId, string permissionName, CancellationToken ct = default)
    {
        var isSuperAdmin = await db.UserRoles
            .Where(x => x.UserId == userId)
            .Join(db.Roles, x => x.RoleId, x => x.Id, (_, role) => role.Name)
            .AnyAsync(name => name == ApplicationRole.Roles.SuperAdmin, ct);

        if (isSuperAdmin)
        {
            return true;
        }

        return await db.UserRoles
            .Where(x => x.UserId == userId)
            .Join(db.RolePermissions, x => x.RoleId, x => x.RoleId, (_, mapping) => mapping.PermissionId)
            .Join(db.Permissions, id => id, permission => permission.Id, (_, permission) => permission.Name)
            .AnyAsync(name => name == permissionName, ct);
    }
}
