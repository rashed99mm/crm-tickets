using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Seeders;

public sealed class PermissionSeeder(AppDbContext db, RoleManager<ApplicationRole> roleManager)
{
    public static readonly IReadOnlyDictionary<string, string> Catalogue = new Dictionary<string, string>
    {
        ["ticket.create"] = "Create support tickets",
        ["ticket.view"] = "View support tickets",
        ["ticket.assign"] = "Assign support tickets",
        ["ticket.update"] = "Update support tickets",
        ["ticket.close"] = "Close support tickets",
        ["customer.view"] = "View customer profiles",
        ["customer.update"] = "Update customer profiles",
        ["report.view"] = "View reports",
        ["report.export"] = "Export reports",
        ["user.manage"] = "Manage users"
    };

    private static readonly IReadOnlyDictionary<string, string[]> DefaultRoles = new Dictionary<string, string[]>
    {
        ["ticket.create"] = [ApplicationRole.Roles.Agent, ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Admin],
        ["ticket.view"] = [ApplicationRole.Roles.Agent, ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Admin],
        ["ticket.assign"] = [ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Admin],
        ["ticket.update"] = [ApplicationRole.Roles.Agent, ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Admin],
        ["ticket.close"] = [ApplicationRole.Roles.Agent, ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Admin],
        ["customer.view"] = [ApplicationRole.Roles.Agent, ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Admin],
        ["customer.update"] = [ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Admin],
        ["report.view"] = [ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Admin],
        ["report.export"] = [ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Admin],
        ["user.manage"] = [ApplicationRole.Roles.Admin]
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var permissions = await db.Permissions.ToDictionaryAsync(x => x.Name, ct);
        foreach (var (name, description) in Catalogue)
        {
            if (!permissions.ContainsKey(name))
            {
                var permission = Permission.Create(name, description);
                db.Permissions.Add(permission);
                permissions[name] = permission;
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            foreach (var entry in db.ChangeTracker.Entries<Permission>().ToList())
            {
                entry.State = EntityState.Detached;
            }

            permissions = await db.Permissions.ToDictionaryAsync(x => x.Name, ct);
            if (Catalogue.Keys.Any(name => !permissions.ContainsKey(name)))
            {
                throw;
            }
        }

        var roleIds = new Dictionary<string, Guid>();
        foreach (var roleName in DefaultRoles.Values.SelectMany(x => x).Distinct())
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is not null) roleIds[roleName] = role.Id;
        }

        var existing = await db.RolePermissions
            .Select(x => new { x.RoleId, x.PermissionId })
            .ToHashSetAsync(ct);

        foreach (var (permissionName, roleNames) in DefaultRoles)
        {
            foreach (var roleName in roleNames)
            {
                if (roleIds.TryGetValue(roleName, out var roleId) &&
                    existing.Add(new { RoleId = roleId, PermissionId = permissions[permissionName].Id }))
                {
                    db.RolePermissions.Add(RolePermission.Create(roleId, permissions[permissionName].Id));
                }
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            foreach (var entry in db.ChangeTracker.Entries<RolePermission>().ToList())
            {
                entry.State = EntityState.Detached;
            }

            var mapped = await db.RolePermissions
                .Select(x => new { x.RoleId, x.PermissionId })
                .ToHashSetAsync(ct);
            if (DefaultRoles.Any(pair => pair.Value.Any(roleName =>
                    roleIds.TryGetValue(roleName, out var roleId) &&
                    !mapped.Contains(new { RoleId = roleId, PermissionId = permissions[pair.Key].Id }))))
            {
                throw;
            }
        }
    }
}
