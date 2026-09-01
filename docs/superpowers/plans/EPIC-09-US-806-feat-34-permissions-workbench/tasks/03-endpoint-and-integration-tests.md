# Task 03 — The endpoint, proven against real SQL (US-806, AC-806.1…AC-806.5, AC-806.7…AC-806.9)

**Files:**
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/PermissionsController.cs:44` (add a fourth action after `Revoke`)
- Test: `backend/tests/CustomerSupport.Tests/Integration/PermissionTests.cs` (modify — 9 tests today, ending at `MissingPermissionRefusesProtectedEndpointWithEnvelope`)

**Interfaces:**
- Consumes: `SetRolePermissionsCommand` / `SetRolePermissionsRequest` (Task 01), `SetAsync` (Task 02),
  `this.ToActionResult(...)` from `CustomerSupport.Api.Shared.Extensions`
  (`PermissionsController.cs:27` shows the call shape), the class-level
  `[Authorize(Policy = "UserManagement")]` at `PermissionsController.cs:20` — which is the whole of
  `AC-806.7`, and is why no new authorization code is written here.
- Consumes (tests): `CrmApiFactory` with `CreateAuthenticatedClientAsync(role)` and
  `EnsureDatabaseAsync()`, exactly as `PermissionTests.cs:18-19` and `:31-32` use them; the
  save/restore-around-mutation pattern at `PermissionTests.cs:36-60`; the concurrency pattern at
  `PermissionTests.cs:105-151`.
- Produces: `PUT /api/admin/permissions/{roleId:guid}` — consumed by the frontend's
  `PermissionApi.setRolePermissions` in frontend Task 08.

**These tests hit a real SQL Server.** They are the only place the `UPDLOCK` read, the transaction
rollback and the composite-key delete are actually exercised; the Task 02 unit tests mock the
service and prove none of it. Both host settings must be present or every request 500s — see
`CLAUDE.md`.

**Restore what you mutate.** Every test here edits seeded `RolePermissions` rows that later tests
and the seeder assertion (`PermissionTests.cs:165-181`, which asserts exactly 10 permissions) depend
on. The existing tests all use `try`/`finally` restore blocks; so must these. Note the
`db.ChangeTracker.Clear()` at `PermissionTests.cs:137-139` — re-adding a still-tracked composite key
trips an identity conflict, which is a real trap in this file, not a hypothetical one.

## Steps

- [ ] **Step 1: Write the failing integration tests**

Append to `backend/tests/CustomerSupport.Tests/Integration/PermissionTests.cs`:

```csharp
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

    /// <summary>
    /// Puts the role's mappings back to <paramref name="permissionIds"/> exactly. Starts from a clean
    /// change tracker for the reason recorded at :137 — re-adding a tracked composite key throws.
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
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~Integration.PermissionTests"
```

Expected: every new test fails with 404/405 (no `PUT` route exists yet). Confirm the pre-existing 9
tests still pass in the same run — if they do not, the fixture is broken and that is what to fix
first.

- [ ] **Step 3: Add the endpoint**

In `PermissionsController.cs`, after the `Revoke` action (line 43-44), add:

```csharp
    /// <summary>
    /// Replaces the role's permission set in one transaction (AC-806.1). The body's
    /// <c>expectedPermissionIds</c> is the set the caller staged from; a mismatch is a 409 rather
    /// than a silent overwrite (AC-806.5).
    /// </summary>
    [HttpPut("{roleId:guid}")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Set(
        Guid roleId, [FromBody] SetRolePermissionsRequest request, CancellationToken ct)
        => this.ToActionResult(await mediator.Send(
            new SetRolePermissionsCommand(roleId, request.PermissionIds, request.ExpectedPermissionIds), ct));
```

Add the using:

```csharp
using CustomerSupport.Application.Features.Admin.Commands.SetRolePermissions;
```

- [ ] **Step 4: Run the integration tests to verify they pass**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~Integration.PermissionTests"
```

Expected: PASS, 18 tests. Paste the output below.

**If `ConcurrentSetsFromTheSameSnapshot_LeaveExactlyOneWinner` is flaky**, do not add a retry and do
not relax the assertion. Run it three times and record the outcomes; a genuine second winner means
the `UPDLOCK` read is not being taken before the comparison, which is a real defect in Task 02's
`SetAsync` and must be fixed there.

- [ ] **Step 5: Run the whole suite**

```bash
cd backend && dotnet test CustomerSupport.slnx
```

Expected: no regressions. The seeder assertion at `PermissionTests.cs:165-181` (exactly 10
permissions) and `RolePermissionMappingWorks` at `:152` are the two most likely to catch a restore
block that did not restore.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.InternalApi/Controllers/PermissionsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/PermissionTests.cs
git commit -m "feat: PUT /api/admin/permissions/{roleId} sets a role's permissions atomically (AC-806.1..AC-806.9)"
```

## Criteria covered

`AC-806.1`, `AC-806.2`, `AC-806.3`, `AC-806.4`, `AC-806.5`, `AC-806.6` (wire level),
`AC-806.7`, `AC-806.8`, `AC-806.9`.

## Test evidence

*Not yet executed.*

## Deviations from the plan

*None yet.*
