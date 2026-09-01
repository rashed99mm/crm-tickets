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

    /// <summary>
    /// Reads a role and its current permission ids. Every test below stages from this, exactly as the
    /// admin screen does — which is what makes `expectedPermissionIds` a realistic value rather than
    /// a test-only construction.
    /// </summary>
    private async Task<(Guid RoleId, List<Guid> PermissionIds)> SnapshotAsync(string roleName)
    {
        await factory.EnsureDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>()
            .FindByNameAsync(roleName);
        var ids = await db.RolePermissions
            .Where(x => x.RoleId == role!.Id)
            .Select(x => x.PermissionId)
            .ToListAsync();
        return (role!.Id, ids);
    }

    private static HttpContent SetBody(IEnumerable<Guid> permissionIds, IEnumerable<Guid> expected)
        => JsonContent.Create(new { permissionIds, expectedPermissionIds = expected });

    /// <summary>
    /// Puts the role's mappings back to <paramref name="permissionIds"/> exactly. Starts from a clean
    /// change tracker: re-adding a tracked composite key throws (see the concurrency test's restore
    /// block above for the same reasoning).
    /// </summary>
    private async Task RestoreAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        var current = await db.RolePermissions.Where(x => x.RoleId == roleId).ToListAsync();
        db.RolePermissions.RemoveRange(current.Where(x => !permissionIds.Contains(x.PermissionId)));
        var currentIds = current.Select(x => x.PermissionId).ToHashSet();
        foreach (var id in permissionIds.Where(id => !currentIds.Contains(id)))
        {
            db.RolePermissions.Add(RolePermission.Create(roleId, id));
        }
        await db.SaveChangesAsync();
    }

    [Fact] // AC-806.1
    [Trait("AC", "806.1")]
    public async Task SetRolePermissions_AppliesAddsAndRemovesInOneCall()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        var (roleId, original) = await SnapshotAsync(ApplicationRole.Roles.Agent);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // One id the role does not hold, and one it does — so a single call must both add and remove.
        var toAdd = await db.Permissions.Where(x => !original.Contains(x.Id)).Select(x => x.Id).FirstAsync();
        var toRemove = original.First();
        var target = original.Where(id => id != toRemove).Append(toAdd).ToList();

        try
        {
            var response = await client.PutAsync(
                $"/api/admin/permissions/{roleId}", SetBody(target, original));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("\"code\":\"CON079\"");

            using var verifyScope = factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await verifyDb.RolePermissions
                .Where(x => x.RoleId == roleId).Select(x => x.PermissionId).ToListAsync();
            stored.Should().BeEquivalentTo(target, "the set is replaced, not merged");
        }
        finally
        {
            await RestoreAsync(roleId, original);
        }
    }

    [Fact] // AC-806.2
    [Trait("AC", "806.2")]
    public async Task SetRolePermissions_CannotEmptyABuiltInRole()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        var (roleId, original) = await SnapshotAsync(ApplicationRole.Roles.Admin);

        var response = await client.PutAsync(
            $"/api/admin/permissions/{roleId}", SetBody(Array.Empty<Guid>(), original));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"code\":\"ERR002\"");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.RolePermissions
            .Where(x => x.RoleId == roleId).Select(x => x.PermissionId).ToListAsync();
        stored.Should().BeEquivalentTo(original, "the refusal is atomic — nothing was removed");
    }

    [Fact] // AC-806.5
    [Trait("AC", "806.5")]
    public async Task SetRolePermissions_RefusesAStaleSnapshot()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        var (roleId, original) = await SnapshotAsync(ApplicationRole.Roles.Agent);
        // An expectation that was never true: one id short of what is stored.
        var staleExpectation = original.Skip(1).ToList();

        var response = await client.PutAsync(
            $"/api/admin/permissions/{roleId}", SetBody(staleExpectation, staleExpectation));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"code\":\"ERR087\"");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.RolePermissions.Where(x => x.RoleId == roleId).CountAsync())
            .Should().Be(original.Count, "a stale save writes nothing");
    }

    [Fact] // AC-806.3
    [Trait("AC", "806.3")]
    public async Task SetRolePermissions_UnknownPermissionIdIsNotFound()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        var (roleId, original) = await SnapshotAsync(ApplicationRole.Roles.Agent);

        var response = await client.PutAsync(
            $"/api/admin/permissions/{roleId}",
            SetBody(original.Append(Guid.NewGuid()), original));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.RolePermissions.Where(x => x.RoleId == roleId).CountAsync()).Should().Be(original.Count);
    }

    [Fact] // AC-806.4
    [Trait("AC", "806.4")]
    public async Task SetRolePermissions_UnknownRoleIsNotFound()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);

        var response = await client.PutAsync(
            $"/api/admin/permissions/{Guid.NewGuid()}",
            SetBody(Array.Empty<Guid>(), Array.Empty<Guid>()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // AC-806.6
    [Trait("AC", "806.6")]
    public async Task SetRolePermissions_MissingExpectedSnapshotIsAFieldKeyedBadRequest()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        var (roleId, original) = await SnapshotAsync(ApplicationRole.Roles.Agent);

        var response = await client.PutAsync(
            $"/api/admin/permissions/{roleId}", JsonContent.Create(new { permissionIds = original }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ExpectedPermissionIds");
        body.Should().Contain("VAL081");
    }

    [Fact] // AC-806.7
    [Trait("AC", "806.7")]
    public async Task SetRolePermissions_IsRefusedForANonAdministrator()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.User);
        var (roleId, original) = await SnapshotAsync(ApplicationRole.Roles.Agent);

        var response = await client.PutAsync(
            $"/api/admin/permissions/{roleId}", SetBody(original, original));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // AC-806.9
    [Trait("AC", "806.9")]
    public async Task SetRolePermissions_NoOpSetSucceedsAndWritesNothing()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        var (roleId, original) = await SnapshotAsync(ApplicationRole.Roles.Agent);

        var response = await client.PutAsync(
            $"/api/admin/permissions/{roleId}", SetBody(original, original));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.RolePermissions
            .Where(x => x.RoleId == roleId).Select(x => x.PermissionId).ToListAsync();
        stored.Should().BeEquivalentTo(original);
    }

    [Fact] // AC-806.8 — the whole reason expectedPermissionIds exists
    [Trait("AC", "806.8")]
    public async Task ConcurrentSetsFromTheSameSnapshot_LeaveExactlyOneWinner()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        var (roleId, original) = await SnapshotAsync(ApplicationRole.Roles.Agent);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var spare = await db.Permissions.Where(x => !original.Contains(x.Id)).Select(x => x.Id).Take(2).ToListAsync();
        spare.Should().HaveCount(2, "the seeded catalogue must offer two ids the Agent role lacks");

        try
        {
            // Both callers staged from `original`; each wants a different result.
            var first = client.PutAsync($"/api/admin/permissions/{roleId}",
                SetBody(original.Append(spare[0]), original));
            var second = client.PutAsync($"/api/admin/permissions/{roleId}",
                SetBody(original.Append(spare[1]), original));
            await Task.WhenAll(first, second);

            var codes = new[] { first.Result.StatusCode, second.Result.StatusCode };
            codes.Count(x => x == HttpStatusCode.OK).Should().Be(1,
                "the second save staged from a snapshot the first invalidated");
            codes.Should().Contain(HttpStatusCode.Conflict);

            using var verifyScope = factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await verifyDb.RolePermissions
                .Where(x => x.RoleId == roleId).Select(x => x.PermissionId).ToListAsync();
            stored.Should().HaveCount(original.Count + 1, "exactly one winner, no interleaved result");
        }
        finally
        {
            await RestoreAsync(roleId, original);
        }
    }

    [Fact] // AC-806.10
    [Trait("AC", "806.10")]
    public async Task SetRolePermissions_WritesAnAuditEntry()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        var (roleId, original) = await SnapshotAsync(ApplicationRole.Roles.Agent);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var toAdd = await db.Permissions.Where(x => !original.Contains(x.Id)).Select(x => x.Id).FirstAsync();
        var before = DateTime.UtcNow.AddSeconds(-1);

        try
        {
            var response = await client.PutAsync(
                $"/api/admin/permissions/{roleId}", SetBody(original.Append(toAdd), original));
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var verifyScope = factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entry = await verifyDb.AuditLogs
                .Where(x => x.EntityType == "Role" && x.EntityId == roleId && x.CreatedAt >= before)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            entry.Should().NotBeNull("a permission change must leave a trail (AC-806.10)");
            entry!.Action.Should().Be("Updated");
            entry.UserId.Should().NotBe(Guid.Empty, "the acting administrator is named");
            entry.NewValues.Should().NotBeNull().And.Contain(toAdd.ToString(),
                "the entry records what the set was changed to");
        }
        finally
        {
            await RestoreAsync(roleId, original);
        }
    }

    [Fact] // AC-806.10 — a refused change is not a change, so it leaves no trail
    [Trait("AC", "806.10")]
    public async Task SetRolePermissions_RefusedChangeWritesNoAuditEntry()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        var (roleId, original) = await SnapshotAsync(ApplicationRole.Roles.Admin);
        var before = DateTime.UtcNow.AddSeconds(-1);

        var response = await client.PutAsync(
            $"/api/admin/permissions/{roleId}", SetBody(Array.Empty<Guid>(), original));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.AuditLogs.CountAsync(x =>
                x.EntityType == "Role" && x.EntityId == roleId && x.CreatedAt >= before))
            .Should().Be(0);
    }
}
