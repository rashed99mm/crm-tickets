using CustomerSupport.Application.Features.Admin.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Security;

public sealed class PermissionAdministrationService(AppDbContext db) : IPermissionAdministrationService
{
    private static readonly IReadOnlySet<string> BuiltInRoles = new HashSet<string>(StringComparer.Ordinal)
    {
        ApplicationRole.Roles.SuperAdmin,
        ApplicationRole.Roles.Admin,
        ApplicationRole.Roles.ContentManager,
        ApplicationRole.Roles.StateRepresentative,
        ApplicationRole.Roles.User,
        ApplicationRole.Roles.Visitor,
        ApplicationRole.Roles.Agent,
        ApplicationRole.Roles.Supervisor
    };

    public async Task<PermissionAdministrationDto> GetAsync(CancellationToken ct = default)
    {
        var permissions = await db.Permissions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new PermissionAdministrationPermissionDto(x.Id, x.Name, x.Description))
            .ToListAsync(ct);

        var roleRows = await db.Roles
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(ct);

        var rolePermissionRows = await db.RolePermissions
            .AsNoTracking()
            .Select(x => new { x.RoleId, x.PermissionId })
            .ToListAsync(ct);
        var permissionIdsByRole = rolePermissionRows
            .GroupBy(x => x.RoleId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<Guid>)x.Select(mapping => mapping.PermissionId).ToList());
        var roles = roleRows
            .Select(x => new PermissionAdministrationRoleDto(
                x.Id,
                x.Name!,
                permissionIdsByRole.TryGetValue(x.Id, out var ids) ? ids : Array.Empty<Guid>()))
            .ToList();

        return new PermissionAdministrationDto(roles, permissions);
    }

    public async Task<PermissionMutationResult> AssignAsync(Guid roleId, Guid permissionId, CancellationToken ct = default)
    {
        if (!await db.Roles.AnyAsync(x => x.Id == roleId, ct)) return PermissionMutationResult.RoleNotFound;
        if (!await db.Permissions.AnyAsync(x => x.Id == permissionId, ct)) return PermissionMutationResult.PermissionNotFound;
        if (await db.RolePermissions.AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, ct))
            return PermissionMutationResult.AlreadyAssigned;

        db.RolePermissions.Add(RolePermission.Create(roleId, permissionId));
        await db.SaveChangesAsync(ct);
        return PermissionMutationResult.Succeeded;
    }

    public async Task<PermissionMutationResult> RevokeAsync(Guid roleId, Guid permissionId, CancellationToken ct = default)
    {
        var role = await db.Roles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == roleId, ct);
        if (role is null) return PermissionMutationResult.RoleNotFound;
        if (!await db.Permissions.AnyAsync(x => x.Id == permissionId, ct)) return PermissionMutationResult.PermissionNotFound;

        var mapping = await db.RolePermissions.SingleOrDefaultAsync(
            x => x.RoleId == roleId && x.PermissionId == permissionId, ct);
        if (mapping is null) return PermissionMutationResult.MappingNotFound;

        // Count-then-delete is only safe under a lock (US-805 AC-805.4): two concurrent revokes
        // must not both observe count > 1 and strip a built-in role to zero mappings. The UPDLOCK
        // hint makes the count take update locks on the role's mapping rows — update locks are
        // incompatible with each other, so a second concurrent revoke blocks until the first
        // commits, re-reads the count, and is refused. The whole unit runs through the retrying
        // execution strategy (EnableRetryOnFailure forbids bare user transactions), so a deadlock
        // victim or transient failure is retried rather than surfaced as a 500.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var assignedCount = await db.RolePermissions
                .FromSqlInterpolated(
                    $"SELECT RoleId, PermissionId FROM RolePermissions WITH (UPDLOCK) WHERE RoleId = {roleId}")
                .CountAsync(ct);
            if (BuiltInRoles.Contains(role.Name!) && assignedCount <= 1)
            {
                await transaction.RollbackAsync(ct);
                return PermissionMutationResult.LastPermissionRequired;
            }

            db.RolePermissions.Remove(mapping);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return PermissionMutationResult.Succeeded;
        });
    }
}
