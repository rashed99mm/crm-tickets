# Task 04 — Audit a permission change (US-806, AC-806.10)

**Files:**
- Modify: `backend/src/CustomerSupport.Application/Behaviors/AuditBehavior.cs:17-38` (register the command) and `:120-133` (`ResolveEntityId`)
- Test: `backend/tests/CustomerSupport.Tests/Integration/PermissionTests.cs` (modify)

**Interfaces:**
- Consumes: `AuditBehavior<TRequest, TResponse>`'s existing machinery — `AuditableCommands`
  (`AuditBehavior.cs:17-23`), `EntityTypeMapping` (`:25-38`), `RecordAsync` (`:82-113`),
  `AuditLog.Create(userId, userName, action, entityType, entityId, oldValues, newValues)`
  (`Domain/Entities/Audit/AuditLog.cs:15-38`), `IAuditService.LogAsync`.
- Produces: nothing new for later tasks. This is the last backend task.

**Why this task exists.** `AuditBehavior.cs:17-23` lists eleven auditable commands. Creating a
*notification* is audited; changing which permissions a role holds is not. That asymmetry is the
spec's Finding 3, and the batch endpoint is the right place to close it because it is the one call
that carries a role's whole permission set — the natural unit for an audit entry.

**The trap that makes this a real task and not a one-line addition.** `RecordAsync` skips the entry
when `ResolveEntityId` returns null (`AuditBehavior.cs:91-96`), and `ResolveEntityId` looks for a
response `Data` of type `Guid` or a **request property named exactly `Id`**
(`AuditBehavior.cs:120-133`). `SetRolePermissionsCommand`'s is `RoleId`, and its response `Data` is
`MediatR.Unit`. So registering the command alone produces **no audit row and no error** — a silent
skip, which is the worst of the three possible outcomes. The resolver needs a `RoleId` fallback.

## Steps

- [ ] **Step 1: Write the failing test**

Append to `backend/tests/CustomerSupport.Tests/Integration/PermissionTests.cs`. It reuses
`SnapshotAsync`, `SetBody` and `RestoreAsync` from Task 03:

```csharp
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
```

The second test needs no new code — `RecordAsync` already returns early on an unsuccessful response
(`AuditBehavior.cs:85-88`). It is here because that behaviour is load-bearing for this criterion and
nothing else asserts it.

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~Integration.PermissionTests"
```

Expected: `SetRolePermissions_WritesAnAuditEntry` FAILS with `entry` null.
`SetRolePermissions_RefusedChangeWritesNoAuditEntry` should already PASS — if it fails, a refusal is
being audited and that is a defect to fix before continuing.

- [ ] **Step 3: Register the command**

In `AuditBehavior.cs`, add to `AuditableCommands` (line 17-23):

```csharp
        "CreatePlatformSettingCommand", "UpdatePlatformSettingCommand", "DeletePlatformSettingCommand",
        // FEAT-34 / AC-806.10 — changing a role's permission set is the most security-relevant
        // administrative action in the system and was, until now, the only one leaving no trail.
        "SetRolePermissionsCommand"
```

and to `EntityTypeMapping` (line 25-38):

```csharp
        { "SetRolePermissionsCommand", "Role" }
```

- [ ] **Step 4: Give `ResolveEntityId` a `RoleId` fallback**

Replace `ResolveEntityId` (`AuditBehavior.cs:120-133`) with:

```csharp
    /// <summary>
    /// A create command's new id is the response's <c>Data</c>; an update/delete command's target
    /// id is a property named <c>Id</c> on the request itself. Both are read by reflection because
    /// this behavior has to work across every auditable command's distinct shape.
    ///
    /// <c>RoleId</c> is checked last: a command whose subject is a role names it that way
    /// (<c>SetRolePermissionsCommand</c>), and without this fallback the entry is skipped silently
    /// at the null check in <see cref="RecordAsync"/> rather than failing loudly (AC-806.10).
    /// </summary>
    private static Guid? ResolveEntityId(TRequest request, TResponse response)
    {
        if (typeof(TResponse).GetProperty("Data")?.GetValue(response) is Guid fromResponse)
        {
            return fromResponse;
        }

        if (typeof(TRequest).GetProperty("Id")?.GetValue(request) is Guid fromRequest)
        {
            return fromRequest;
        }

        if (typeof(TRequest).GetProperty("RoleId")?.GetValue(request) is Guid fromRoleId)
        {
            return fromRoleId;
        }

        return null;
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~Integration.PermissionTests"
```

Expected: PASS, 20 tests. Paste the output below.

- [ ] **Step 6: Run the whole suite and check the build**

```bash
cd backend && dotnet test CustomerSupport.slnx && dotnet build CustomerSupport.slnx
```

Expected: no regressions, `Build succeeded` with 0 warnings. The `RoleId` fallback is reached by
**every** auditable command via reflection, so watch for an unexpected new audit row in the user or
content tests — none of those commands has a `RoleId`, but confirm rather than assume.

- [ ] **Step 7: Commit**

```bash
git add backend/src/CustomerSupport.Application/Behaviors/AuditBehavior.cs \
        backend/tests/CustomerSupport.Tests/Integration/PermissionTests.cs
git commit -m "feat: audit role permission changes (AC-806.10)"
```

## Not done here, deliberately

The two single-mapping endpoints (`AssignPermissionCommand`, `RevokePermissionCommand`) remain
unaudited. Adding them is one string each in `AuditableCommands` plus one `EntityTypeMapping` entry
each — but their commands' subject id is `RoleId` too, so they would work through the same fallback
this task adds. It is left out because it is not in any `AC-n` and the screen no longer calls them
after frontend Task 08. **If the human partner wants it, it is a five-line follow-up task, not a
redesign** — recorded here so the choice is visible rather than forgotten.

## Criteria covered

`AC-806.10`.

## Test evidence

**BLOCKED — not verified**, for the same reason recorded in Task 03: the integration test
environment cannot get permission seeding to run in this sandbox (evidenced by pre-existing,
untouched tests failing identically). The code — the `AuditableCommands`/`EntityTypeMapping`
registrations and the `RoleId` fallback in `ResolveEntityId` — is written and compiles, but
`SetRolePermissions_WritesAnAuditEntry` and `SetRolePermissions_RefusedChangeWritesNoAuditEntry`
have not been run to a pass.

## Deviations from the plan

None in the code itself. See Task 03 for the shared environment blocker.
