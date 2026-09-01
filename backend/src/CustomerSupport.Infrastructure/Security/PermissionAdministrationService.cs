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

    public async Task<PermissionMutationResult> SetAsync(
        Guid roleId,
        IReadOnlyCollection<Guid> permissionIds,
        IReadOnlyCollection<Guid> expectedPermissionIds,
        CancellationToken ct = default)
    {
        var role = await db.Roles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == roleId, ct);
        if (role is null) return PermissionMutationResult.RoleNotFound;

        var requested = permissionIds.ToHashSet();

        // AC-806.3 — every id must name a real permission. Checked outside the transaction: the
        // catalogue is seeded and is not what the lock below defends against.
        if (requested.Count > 0)
        {
            var known = await db.Permissions.CountAsync(x => requested.Contains(x.Id), ct);
            if (known != requested.Count) return PermissionMutationResult.PermissionNotFound;
        }

        // AC-806.2 — a built-in role may never be emptied. Cheap pre-check for the obvious case;
        // re-asserted inside the lock below, because "would this leave it empty" is a question about
        // state a concurrent writer can move.
        if (requested.Count == 0 && BuiltInRoles.Contains(role.Name!))
            return PermissionMutationResult.LastPermissionRequired;

        // Same shape as RevokeAsync (:83-101) and for the same reason: EnableRetryOnFailure forbids
        // bare user transactions, so the transaction runs inside the retrying execution strategy.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // The UPDLOCK read is taken BEFORE any decision, so a second concurrent save blocks
            // here, then re-reads and finds its expected set no longer current (AC-806.8). Both
            // mapped columns are selected, so these materialise as tracked entities and can be
            // removed directly (RolePermission has no other property — RolePermission.cs:5-11).
            var current = await db.RolePermissions
                .FromSqlInterpolated(
                    $"SELECT RoleId, PermissionId FROM RolePermissions WITH (UPDLOCK) WHERE RoleId = {roleId}")
                .ToListAsync(ct);
            var currentIds = current.Select(x => x.PermissionId).ToHashSet();

            // AC-806.5 — order-insensitive set equality (spec A4). A stale save is refused, never
            // merged: merging is how a revoke silently un-revokes itself (spec A6).
            if (!currentIds.SetEquals(expectedPermissionIds))
            {
                await transaction.RollbackAsync(ct);
                return PermissionMutationResult.StaleSnapshot;
            }

            if (requested.Count == 0 && BuiltInRoles.Contains(role.Name!))
            {
                await transaction.RollbackAsync(ct);
                return PermissionMutationResult.LastPermissionRequired;
            }

            var toRemove = current.Where(x => !requested.Contains(x.PermissionId)).ToList();
            var toAdd = requested.Where(id => !currentIds.Contains(id)).ToList();

            // AC-806.9 — a no-op set writes nothing at all, rather than deleting and re-inserting
            // the same rows.
            if (toRemove.Count == 0 && toAdd.Count == 0)
            {
                await transaction.RollbackAsync(ct);
                return PermissionMutationResult.Succeeded;
            }

            db.RolePermissions.RemoveRange(toRemove);
            foreach (var permissionId in toAdd)
            {
                db.RolePermissions.Add(RolePermission.Create(roleId, permissionId));
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return PermissionMutationResult.Succeeded;
        });
    }
}
