> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier � this plan did not precede its implementation.

# US-804 Permission Entity: Implementation Plan

> **Disclosure (added 2026-08-27):** Rewritten to carry real, code-bearing Task sections. The
> `Permission` / `RolePermission` entities and the `PermissionSeeder` **already exist in the tree**
> (implemented during the auth/authorization passes). This plan now quotes that shipped code
> accurately. The cited ACs remain **unverified by a named test** — that is the residual gap, not the
> absence of implementation.

**Story:** `EPIC-09-US-804-permission-entity`
**Spec:** `docs/superpowers/specs/EPIC-09-EPIC-09-US-804-permission-entity.md`
**Status:** PARTIAL — implementation exists in tree (unverified by a named test).

## Affected files (real)

- `backend/src/CustomerSupport.Domain/Entities/Identity/Permission.cs`
- `backend/src/CustomerSupport.Domain/Entities/Identity/RolePermission.cs`
- `backend/src/CustomerSupport.Infrastructure/Seeders/PermissionSeeder.cs`

---

### Task 1: The `Permission` entity (shipped, quoted) — `AC-804.1`

**Files:**
- Real: `backend/src/CustomerSupport.Domain/Entities/Identity/Permission.cs`

- [ ] **Step 1: Real entity code (in tree)**

```csharp
namespace CustomerSupport.Domain.Entities.Identity;

public sealed class Permission
{
    private Permission() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;   // e.g. "ticket.create"
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static Permission Create(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            throw new ArgumentException("Permission name must contain 1 to 100 characters.", nameof(name));
        return new Permission
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

- [ ] **Step 2: No production change required** — the entity is in the tree; this task records it.

- [ ] **Step 3: Residual — add a named test (the unverified gap)**

```csharp
// backend/tests/CustomerSupport.Tests/Unit/PermissionEntityTests.cs
[Fact] [Trait("AC", "804.1")]
public void AC804_1_Create_ValidName_BuildsEntity()
{
    var p = Permission.Create("report.view", "View reports");
    p.Name.Should().Be("report.view");
    p.Id.Should().NotBe(Guid.Empty);
}

[Fact] [Trait("AC", "804.1")]
public void AC804_1_Create_EmptyName_Throws()
    => FluentActions.Invoking(() => Permission.Create(" ")).Should().Throw<ArgumentException>();
```

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~PermissionEntityTests"`
Expected: PASS — closes the unverified gap.

- [ ] **Step 4: Commit**

```bash
git add backend/tests/CustomerSupport.Tests/Unit/PermissionEntityTests.cs
git commit -m "test(permissions): entity unit tests (AC-804.1)"
```

---

### Task 2: The `RolePermission` join + seeder (shipped, quoted) — `AC-804.2`, `AC-804.3`

**Files:**
- Real: `RolePermission.cs`, `PermissionSeeder.cs`

- [ ] **Step 1: Real join entity (in tree)**

```csharp
public sealed class RolePermission
{
    private RolePermission() { }
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public ApplicationRole Role { get; private set; } = null!;
    public Permission Permission { get; private set; } = null!;
    public static RolePermission Create(Guid roleId, Guid permissionId) => new()
        { RoleId = roleId, PermissionId = permissionId };
}
```

- [ ] **Step 2: Real seeder (in tree) — idempotent, conflict-tolerant**

```csharp
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
// DefaultRoles maps each permission name -> [Agent, Supervisor, Admin] subsets; SeedAsync is
// wrapped in try/catch(DbUpdateException) that detaches and re-checks so concurrent seeding never
// 500s. It creates missing Permissions, then adds missing RolePermission rows for each role.
```

- [ ] **Step 3: Residual — named integration test for the seeder**

```csharp
[Fact] [Trait("AC", "804.3")]
public async Task AC804_3_Seed_CreatesAllCataloguePermissions()
{
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    await new PermissionSeeder(db, roleManager).SeedAsync();
    (await db.Permissions.CountAsync()).Should().Be(PermissionSeeder.Catalogue.Count);
}
```

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC804_3"`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add backend/tests/CustomerSupport.Tests/Integration/PermissionSeederTests.cs
git commit -m "test(permissions): seeder integration test (AC-804.3)"
```

## Definition of done

- [x] `Permission` entity in tree (`AC-804.1`).
- [x] `RolePermission` + `PermissionSeeder` in tree (`AC-804.2`, `AC-804.3`).
- [ ] Cited ACs verified by named tests (Task 1/2 residual tests close the gap).
- [x] No dependency-rule violation: `Permission`/`RolePermission` live in `Domain`, depend on nothing.

## Deviation record

Implementation preceded this plan (auth/authorization passes). The only outstanding item is
story-named test verification; this rewrite adds those tests as the closing task.

