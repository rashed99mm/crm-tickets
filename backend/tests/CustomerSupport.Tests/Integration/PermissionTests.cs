using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace CustomerSupport.Tests.Integration;

public sealed class PermissionTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    [Fact] // AC-805.1
    public async Task AdministratorCanListPermissionsAndRoleAssignments()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);

        var response = await client.GetAsync("/api/admin/permissions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ticket.create");
        body.Should().Contain(ApplicationRole.Roles.Admin);
    }

    [Fact] // AC-805.2, AC-805.3
    public async Task AdministratorCanAssignAndRevokePermission()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        await factory.EnsureDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>()
            .FindByNameAsync(ApplicationRole.Roles.Admin);
        var permission = await db.Permissions.SingleAsync(x => x.Name == "ticket.create");
        var mapping = await db.RolePermissions.SingleOrDefaultAsync(x => x.RoleId == role!.Id && x.PermissionId == permission.Id);
        if (mapping is not null)
        {
            db.RolePermissions.Remove(mapping);
            await db.SaveChangesAsync();
        }

        try
        {
            (await client.PostAsync($"/api/admin/permissions/{role.Id}/{permission.Id}", JsonContent.Create(new { })))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.DeleteAsync($"/api/admin/permissions/{role.Id}/{permission.Id}"))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            if (!await db.RolePermissions.AnyAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id))
            {
                db.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact] // AC-805.1..AC-805.3
    public async Task NonAdministratorCannotManagePermissions()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.User);

        var response = await client.GetAsync("/api/admin/permissions");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // AC-805.4
    public async Task LastPermissionOnBuiltInRoleIsRejected()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        await factory.EnsureDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>()
            .FindByNameAsync(ApplicationRole.Roles.Admin);
        var mappings = await db.RolePermissions.Where(x => x.RoleId == role!.Id).ToListAsync();
        var retained = mappings.First();
        db.RolePermissions.RemoveRange(mappings.Skip(1));
        await db.SaveChangesAsync();

        try
        {
            var response = await client.DeleteAsync($"/api/admin/permissions/{role.Id}/{retained.PermissionId}");
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var body = await response.Content.ReadAsStringAsync();
            // The internal key PERMISSION_LAST_REQUIRED is mapped to the stable wire code ERR002
            // (SystemCodeMap) and a localized message — the envelope exposes ERR002, not the key.
            body.Should().Contain("\"code\":\"ERR002\"");
        }
        finally
        {
            foreach (var mapping in mappings.Skip(1))
            {
                if (!await db.RolePermissions.AnyAsync(x => x.RoleId == mapping.RoleId && x.PermissionId == mapping.PermissionId))
                    db.RolePermissions.Add(RolePermission.Create(mapping.RoleId, mapping.PermissionId));
            }
            await db.SaveChangesAsync();
        }
    }

    [Fact] // AC-805.4 — the last-permission guard must hold under concurrency, not just sequentially
    public async Task ConcurrentRevokesLeaveBuiltInRoleWithOneMapping()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        await factory.EnsureDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>()
            .FindByNameAsync(ApplicationRole.Roles.Admin);
        var mappings = await db.RolePermissions.Where(x => x.RoleId == role!.Id).ToListAsync();
        // Exactly two mappings: if both concurrent revokes win, the role is stripped to zero.
        var pair = mappings.Take(2).ToList();
        db.RolePermissions.RemoveRange(mappings.Skip(2));
        await db.SaveChangesAsync();

        try
        {
            var first = client.DeleteAsync($"/api/admin/permissions/{role.Id}/{pair[0].PermissionId}");
            var second = client.DeleteAsync($"/api/admin/permissions/{role.Id}/{pair[1].PermissionId}");
            await Task.WhenAll(first, second);

            var codes = new[] { first.Result.StatusCode, second.Result.StatusCode };
            codes.Count(x => x == HttpStatusCode.OK).Should().Be(1,
                "the serialized guard refuses the second revoke instead of stripping the role");
            codes.Should().Contain(HttpStatusCode.Conflict);

            (await db.RolePermissions.CountAsync(x => x.RoleId == role.Id))
                .Should().BeGreaterThanOrEqualTo(1);
        }
        finally
        {
            // The pair entities are still tracked (Unchanged); re-adding fresh instances with the
            // same keys would trip identity conflict. Start the restore from a clean tracker.
            db.ChangeTracker.Clear();
            foreach (var mapping in mappings.Skip(2))
            {
                if (!await db.RolePermissions.AnyAsync(x => x.RoleId == mapping.RoleId && x.PermissionId == mapping.PermissionId))
                    db.RolePermissions.Add(RolePermission.Create(mapping.RoleId, mapping.PermissionId));
            }
            foreach (var removed in pair)
            {
                if (!await db.RolePermissions.AnyAsync(x => x.RoleId == removed.RoleId && x.PermissionId == removed.PermissionId))
                    db.RolePermissions.Add(RolePermission.Create(removed.RoleId, removed.PermissionId));
            }
            await db.SaveChangesAsync();
        }
    }

    [Fact] // AC-804.2
    public async Task RolePermissionMappingWorks()
    {
        await factory.EnsureDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>()
            .FindByNameAsync(ApplicationRole.Roles.Admin);

        var permission = await db.Permissions.SingleAsync(x => x.Name == "ticket.create");
        (await db.RolePermissions.AnyAsync(x => x.RoleId == role!.Id && x.PermissionId == permission.Id))
            .Should().BeTrue();
    }

    [Fact] // AC-804.3
    public async Task PermissionsSeededOnStartupAndRemainIdempotent()
    {
        await factory.EnsureDatabaseAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Permissions.CountAsync()).Should().BeGreaterThanOrEqualTo(10);
        }

        // The internal host runs the same seeder on startup; duplicate keys/mappings are rejected by
        // the database constraints, so this verifies the catalogue is stable after startup.
        using var secondScope = factory.Services.CreateScope();
        await secondScope.ServiceProvider.GetRequiredService<CustomerSupport.Infrastructure.Seeders.PermissionSeeder>().SeedAsync();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await secondDb.Permissions.CountAsync()).Should().Be(10);
    }

    [Fact] // AC-804.1
    public async Task PermissionNamesAreUnique()
    {
        await factory.EnsureDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Permissions.Add(Permission.Create("ticket.create"));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact] // AC-804.3
    public async Task MissingPermissionRefusesProtectedEndpointWithEnvelope()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>()
            .FindByNameAsync(ApplicationRole.Roles.Admin);
        var permission = await db.Permissions.SingleAsync(x => x.Name == "user.manage");
        var mapping = await db.RolePermissions.SingleAsync(x => x.RoleId == role!.Id && x.PermissionId == permission.Id);
        db.RolePermissions.Remove(mapping);
        await db.SaveChangesAsync();

        try
        {
            var response = await client.GetAsync("/api/admin/audit-log");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await response.Content.ReadAsStringAsync()).Should().Contain("success");
        }
        finally
        {
            db.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
            await db.SaveChangesAsync();
        }
    }
}
