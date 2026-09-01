# Task 02 — Atomic set-replace: `SetAsync` + handler (US-806, AC-806.2…AC-806.5, AC-806.9)

**Files:**
- Modify: `backend/src/CustomerSupport.Application/Interfaces/IPermissionAdministrationService.cs:5-20` (add `StaleSnapshot` to the enum, `SetAsync` to the interface)
- Create: `backend/src/CustomerSupport.Application/Features/Admin/Commands/SetRolePermissions/SetRolePermissionsCommandHandler.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/Security/PermissionAdministrationService.cs` (add `SetAsync` after `RevokeAsync`, which ends at line 102)
- Test: `backend/tests/CustomerSupport.Tests/Unit/Features/Admin/PermissionAdministrationTests.cs` (modify)

**Interfaces:**
- Consumes: `SetRolePermissionsCommand` (Task 01), `ApplicationErrors.Permission.*` (Task 01),
  `IMessageFactory.NotFound<T>` / `.Fail<T>(key, MessageType)` / `.Success<T>(data, key)`
  (`Application/Messages/IMessageFactory.cs:7-13`), `RolePermission.Create(roleId, permissionId)`
  (`Domain/Entities/Identity/RolePermission.cs:13`), and the locking shape at
  `PermissionAdministrationService.cs:83-101`.
- Produces (Task 03 and Task 04 rely on these exact names):
  - `PermissionMutationResult.StaleSnapshot`
  - `Task<PermissionMutationResult> IPermissionAdministrationService.SetAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, IReadOnlyCollection<Guid> expectedPermissionIds, CancellationToken ct = default)`
  - `sealed class SetRolePermissionsCommandHandler(IPermissionAdministrationService, IMessageFactory) : ICommandHandler<SetRolePermissionsCommand, Response<Unit>>`

**Grounding note that changed the plan.** `RolePermission`
(`Domain/Entities/Identity/RolePermission.cs:5-11`) has exactly two scalar columns and a composite
key `{RoleId, PermissionId}` (`RolePermissionConfiguration.cs:12`). That is what makes
`SELECT RoleId, PermissionId FROM RolePermissions WITH (UPDLOCK)` safe to materialise as **tracked
entities** — EF Core requires a `FromSql` query to return every mapped property, and here those two
columns are every mapped property. `RevokeAsync` only counts that query (`:87-90`); `SetAsync` needs
the rows themselves to delete them, and can have them for free. No second read, no `AsNoTracking`.

**Refusal order inside the lock, most specific first:** unknown role → unknown permission id → stale
snapshot → built-in floor. Role and permission existence are checked *before* the transaction
because they cannot be changed by the concurrent writer this lock is defending against; the
staleness and floor checks must be inside it.

## Steps

- [ ] **Step 1: Write the failing handler tests**

Append to `backend/tests/CustomerSupport.Tests/Unit/Features/Admin/PermissionAdministrationTests.cs`.
These mirror the existing `Revoke_LastBuiltInPermission_ReturnsConflict` at line 16 — a mocked
service, a mocked message factory, and one assertion per mapped outcome:

```csharp
    private static (Mock<IPermissionAdministrationService> Service, Mock<IMessageFactory> Messages) SetMocks(
        PermissionMutationResult outcome)
    {
        var service = new Mock<IPermissionAdministrationService>();
        service.Setup(x => x.SetAsync(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);

        var messages = new Mock<IMessageFactory>();
        messages.Setup(x => x.NotFound<MediatR.Unit>(It.IsAny<string>()))
            .Returns((string key) => Response<MediatR.Unit>.Fail(key, key, MessageType.NotFound));
        messages.Setup(x => x.Fail<MediatR.Unit>(It.IsAny<string>(), It.IsAny<MessageType>()))
            .Returns((string key, MessageType type) => Response<MediatR.Unit>.Fail(key, key, type));
        messages.Setup(x => x.Success(It.IsAny<MediatR.Unit>(), It.IsAny<string>()))
            .Returns((MediatR.Unit data, string key) => Response<MediatR.Unit>.Ok(data, key, key));

        return (service, messages);
    }

    private static Task<Response<MediatR.Unit>> HandleSet(PermissionMutationResult outcome)
    {
        var (service, messages) = SetMocks(outcome);
        return new SetRolePermissionsCommandHandler(service.Object, messages.Object).Handle(
            new SetRolePermissionsCommand(Guid.NewGuid(), [Guid.NewGuid()], []),
            CancellationToken.None);
    }

    [Fact] // AC-806.1
    [Trait("AC", "806.1")]
    public async Task Set_Succeeded_ReturnsUpdatedConfirmation()
    {
        var result = await HandleSet(PermissionMutationResult.Succeeded);

        result.Success.Should().BeTrue();
        result.Code.Should().Be(ApplicationErrors.Permission.UPDATED);
    }

    [Fact] // AC-806.4
    [Trait("AC", "806.4")]
    public async Task Set_RoleNotFound_ReturnsNotFound()
    {
        var result = await HandleSet(PermissionMutationResult.RoleNotFound);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Permission.ROLE_NOT_FOUND);
    }

    [Fact] // AC-806.3
    [Trait("AC", "806.3")]
    public async Task Set_UnknownPermission_ReturnsNotFound()
    {
        var result = await HandleSet(PermissionMutationResult.PermissionNotFound);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Permission.NOT_FOUND);
    }

    [Fact] // AC-806.5
    [Trait("AC", "806.5")]
    public async Task Set_StaleSnapshot_ReturnsConflict()
    {
        var result = await HandleSet(PermissionMutationResult.StaleSnapshot);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Permission.STALE_SNAPSHOT);
        result.MessageType.Should().Be(MessageType.Conflict);
    }

    [Fact] // AC-806.2
    [Trait("AC", "806.2")]
    public async Task Set_WouldEmptyBuiltInRole_ReturnsConflict()
    {
        var result = await HandleSet(PermissionMutationResult.LastPermissionRequired);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Permission.LAST_REQUIRED);
        result.MessageType.Should().Be(MessageType.Conflict);
    }
```

**Before running:** confirm the exact factory names on `Response<T>` —
`Response<T>.Fail(code, message, type)` / `Response<T>.Ok(...)` and the `MessageType` property name
are used above from the existing test at `PermissionAdministrationTests.cs:22-23`, which calls
`Response<MediatR.Unit>.Fail(key, "last", MessageType.Conflict)`. If `Ok` is spelled `Success` in
`Application/Contracts/Response.cs`, use that spelling — do not add an overload to make the test
compile.

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~PermissionAdministrationTests"
```

Expected: compile failure — `SetRolePermissionsCommandHandler`, `SetAsync` and
`PermissionMutationResult.StaleSnapshot` do not exist.

- [ ] **Step 3: Extend the port**

`Application/Interfaces/IPermissionAdministrationService.cs` — add the enum member last so no
existing numeric value shifts:

```csharp
public enum PermissionMutationResult
{
    Succeeded,
    AlreadyAssigned,
    RoleNotFound,
    PermissionNotFound,
    MappingNotFound,
    LastPermissionRequired,
    StaleSnapshot
}

public interface IPermissionAdministrationService
{
    Task<PermissionAdministrationDto> GetAsync(CancellationToken ct = default);
    Task<PermissionMutationResult> AssignAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
    Task<PermissionMutationResult> RevokeAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);

    /// <summary>
    /// Replaces the role's permission set with <paramref name="permissionIds"/> in one transaction
    /// (AC-806.1). Refuses with <see cref="PermissionMutationResult.StaleSnapshot"/> when the stored
    /// set does not set-equal <paramref name="expectedPermissionIds"/> (AC-806.5), and with
    /// <see cref="PermissionMutationResult.LastPermissionRequired"/> when the request would leave a
    /// built-in role with nothing (AC-806.2). Either refusal writes nothing.
    /// </summary>
    Task<PermissionMutationResult> SetAsync(
        Guid roleId,
        IReadOnlyCollection<Guid> permissionIds,
        IReadOnlyCollection<Guid> expectedPermissionIds,
        CancellationToken ct = default);
}
```

- [ ] **Step 4: Write the handler**

Create `SetRolePermissionsCommandHandler.cs` — the switch mirrors
`RevokePermissionCommandHandler.cs:18-26`:

```csharp
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;

namespace CustomerSupport.Application.Features.Admin.Commands.SetRolePermissions;

public sealed class SetRolePermissionsCommandHandler(
    IPermissionAdministrationService permissions,
    IMessageFactory messages)
    : ICommandHandler<SetRolePermissionsCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(SetRolePermissionsCommand request, CancellationToken ct)
    {
        // Both lists are non-null here: ResponseValidationBehavior short-circuits on the validator's
        // NotNull rules before any handler runs (ResponseValidationBehavior.cs:25).
        var result = await permissions.SetAsync(
            request.RoleId, request.PermissionIds!, request.ExpectedPermissionIds!, ct);

        return result switch
        {
            PermissionMutationResult.RoleNotFound =>
                messages.NotFound<Unit>(ApplicationErrors.Permission.ROLE_NOT_FOUND),
            PermissionMutationResult.PermissionNotFound =>
                messages.NotFound<Unit>(ApplicationErrors.Permission.NOT_FOUND),
            PermissionMutationResult.StaleSnapshot =>
                messages.Fail<Unit>(ApplicationErrors.Permission.STALE_SNAPSHOT, MessageType.Conflict),
            PermissionMutationResult.LastPermissionRequired =>
                messages.Fail<Unit>(ApplicationErrors.Permission.LAST_REQUIRED, MessageType.Conflict),
            _ => messages.Success(Unit.Value, ApplicationErrors.Permission.UPDATED)
        };
    }
}
```

- [ ] **Step 5: Implement `SetAsync`**

Append to `Infrastructure/Security/PermissionAdministrationService.cs`, after `RevokeAsync` (which
closes at line 102):

```csharp
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
```

- [ ] **Step 6: Run the unit tests to verify they pass**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~PermissionAdministrationTests"
```

Expected: PASS, 13 tests (2 pre-existing + 6 from Task 01 + 5 here). Paste the output below.

- [ ] **Step 7: Verify the build is clean**

```bash
cd backend && dotnet build CustomerSupport.slnx
```

Expected: `Build succeeded`, 0 warnings.

- [ ] **Step 8: Commit**

```bash
git add backend/src/CustomerSupport.Application/Interfaces/IPermissionAdministrationService.cs \
        backend/src/CustomerSupport.Application/Features/Admin/Commands/SetRolePermissions/SetRolePermissionsCommandHandler.cs \
        backend/src/CustomerSupport.Infrastructure/Security/PermissionAdministrationService.cs \
        backend/tests/CustomerSupport.Tests/Unit/Features/Admin/PermissionAdministrationTests.cs
git commit -m "feat: replace a role's permission set atomically (AC-806.2..AC-806.5, AC-806.9)"
```

## Criteria covered

`AC-806.2`, `AC-806.3`, `AC-806.4`, `AC-806.5`, `AC-806.9` at unit level. Each is re-proven against
real SQL in Task 03 — the locking and the transaction rollback are exactly the parts a mocked test
cannot vouch for.

## Test evidence

Implemented 2026-09-01, in the same commit as Task 01 (same test file). Unit-level result:

```
Passed!  - Failed:     0, Passed:    13, Skipped:     0, Total:    13, Duration: 400 ms - CustomerSupport.Tests.dll (net10.0)
```

**Not yet proven against real SQL.** The `UPDLOCK` read, the transaction rollback and the
composite-key delete in `SetAsync` are exactly what a mocked unit test cannot verify — that
verification is Task 03's integration suite, and it is currently **blocked**; see Task 03's Test
evidence for the full account. `SetAsync`'s code is written and compiles and builds clean, but is
unverified end-to-end as of this entry.

## Deviations from the plan

None beyond the shared `Response<T>`/`MessageType` finding recorded in Task 01 (this task's handler
switch was written exactly as planned once that finding was known).
