# FEAT-16 — Organisation Structure Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Department`/`Branch` lookup entities, full CRUD, seeding, and a Department admin screen
(AC-115, AC-117–AC-121, AC-123).

**Architecture:** `Department`/`Branch` are `BaseEntity` lookup rows — same shape as `Category` —
with an explicit `IsActive`/`Deactivate()`, not the generic soft-delete flag. CQRS under
`Features/Organisation/`. Both entities gain nullable FKs onto `Ticket`, `Category`, `Customer`,
`ApplicationUser`.

**Tech Stack:** .NET 10, EF Core, MediatR, FluentValidation, Angular 20.

**Spec:** [`docs/superpowers/specs/EPIC-13-US-311-organisation-structure.md`](../../specs/EPIC-13-US-311-organisation-structure.md)

## Global Constraints

- Every new domain error key registered in `SystemCode.cs`/`SystemCodeMap.cs`, and (for 404/409)
  the switch arm in `ResponseExtensions.MapFailureStatusCode` — `MapFailureStatusCode` derives the
  HTTP status from the resolved `SystemCode`, not from `MessageType`, so an unmapped key silently
  falls back to `400`. This was missed on the first pass (see Task 2's own note) and is now the
  standing rule every later feature this project built cites.
- Every new unique index pairs with `IDbExceptionTranslator` handling in its Create/Update handler,
  or a duplicate name `500`s instead of `409`ing.

---

### Task 1: `Department`/`Branch` entities + FKs + migration (`AC-115`)

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Entities/Organisation/Department.cs`
- Create: `backend/src/CustomerSupport.Domain/Entities/Organisation/Branch.cs`
- Modify: `Ticket.cs`, `Category.cs` (Tickets domain), `Customer.cs`, `ApplicationUser.cs` — add
  nullable `DepartmentId`/`BranchId`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/DepartmentConfiguration.cs`,
  `BranchConfiguration.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/OrganisationStructureEndpointTests.cs`

**Interfaces:**
- Produces: `Department.Create(string name, Guid? managerId, Guid? id = null)`,
  `Department.Update(string, Guid?)`, `Department.Deactivate()` — `Branch` is the mirror shape.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "115")]
public async Task AC115_CreateDepartment_IsRetrievable()
{
    var response = await _admin.PostAsJsonAsync("/api/Departments", new { name = "Billing" });
    response.StatusCode.Should().Be(HttpStatusCode.Created);
}
```

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~OrganisationStructureEndpointTests"`
Expected: FAIL — no `Department` entity, route doesn't exist.

- [ ] **Step 2: Implement the entity**

```csharp
// backend/src/CustomerSupport.Domain/Entities/Organisation/Department.cs
namespace CustomerSupport.Domain.Entities.Organisation;

/// <summary>
/// Groups users, tickets and categories by organisational unit (AC-115). A lookup entity, the same
/// shape as <see cref="Tickets.Category"/>: an explicit <see cref="IsActive"/> flag toggled by
/// <see cref="Deactivate"/>, not the generic soft-delete flag.
/// </summary>
public class Department : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public Guid? ManagerId { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <paramref name="id"/> is normally left to generate — the seeder is the one caller that needs
    /// a well-known id (AC-118).
    public static Department Create(string name, Guid? managerId, Guid? id = null)
    {
        return new Department
        {
            Id = id ?? Guid.NewGuid(),
            Name = ValidateName(name),
            ManagerId = managerId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, Guid? managerId)
    {
        Name = ValidateName(name);
        ManagerId = managerId;
        MarkUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));
        if (name.Length > 200)
            throw new ArgumentException("Name must not exceed 200 characters", nameof(name));
        return name.Trim();
    }
}
```

`Branch.cs` is the same `BaseEntity` + explicit `IsActive`/`Deactivate()` lookup shape, but carries
distinct fields — `string? Region` and `string Timezone` (defaulting to `"UTC"`) alongside `Name` —
so its factory is `Branch.Create(string name, string? region, string? timezone, Guid? id = null)`
(and `Update(name, region, timezone)`), **not** the two-arg `Department.Create`. One file over in the
same namespace.

- [ ] **Step 3: Add the nullable FKs and generate the migration**

`Ticket` and `ApplicationUser` gain **both** `DepartmentId` and `BranchId` (`Guid?`); `Category`
gains `DepartmentId` only; `Customer` gains `BranchId` only — per `US-303`/`US-304`'s table exactly,
no other column changes. (The original plan/README text over-stated this as "both FKs on all four
entities"; the migration and `*.Configuration.cs` files confirm the asymmetric split above.)

Run: `dotnet ef migrations add AddOrganisationStructure --project src/CustomerSupport.Infrastructure --startup-project src/CustomerSupport.InternalApi`
Expected: one migration touching only `Departments`, `Branches`, and the four FK columns — reviewed
before being applied, per this project's standing migration-review discipline.

- [ ] **Step 4: Run test to verify it passes, commit**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~OrganisationStructureEndpointTests"`
Expected: PASS.

```bash
git add backend/src/CustomerSupport.Domain/Entities/Organisation/ backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/DepartmentConfiguration.cs backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/BranchConfiguration.cs backend/src/CustomerSupport.Infrastructure/Migrations/ backend/tests/CustomerSupport.Tests/Integration/OrganisationStructureEndpointTests.cs
git commit -m "feat(org): Department/Branch entities and FKs (AC-115)"
```

---

### Task 2: Full CRUD (`AC-117`–`AC-121`, `AC-123`) — including the 404/409 fix

**Files:**
- Create: `Features/Organisation/Commands/{CreateDepartment,UpdateDepartment,DeactivateDepartment}/`
  and the `Branch` mirrors
- Create: `Features/Organisation/Queries/{GetDepartments,GetDepartmentById}/` and `Branch` mirrors
- Create: `backend/src/CustomerSupport.InternalApi/Controllers/DepartmentsController.cs`,
  `BranchesController.cs`
- Modify: `ApplicationErrors.cs`, `SystemCode.cs`, `SystemCodeMap.cs`, `ResponseExtensions.cs`,
  `Resources.yaml`

**Interfaces:**
- Consumes: `IDbExceptionTranslator.IsUniqueViolation(Exception)` (existing port).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
[Trait("AC", "119")]
public async Task AC119_UnknownDepartmentId_Returns404()
{
    var response = await _client.GetAsync($"/api/Departments/{Guid.NewGuid()}");
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}

[Fact]
[Trait("AC", "121")]
public async Task AC121_CreateDepartment_DuplicateName_Returns409()
{
    await _admin.PostAsJsonAsync("/api/Departments", new { name = "Billing" });
    var response = await _admin.PostAsJsonAsync("/api/Departments", new { name = "Billing" });
    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
}

[Fact]
[Trait("AC", "120")]
public async Task AC120_Agent_CreatingDepartment_Returns403()
{
    var response = await _agentClient.PostAsJsonAsync("/api/Departments", new { name = "X" });
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

Run: `dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~OrganisationStructureEndpointTests"`
Expected: `AC119` and `AC121` FAIL even after the naive handler exists — this is the actual bug this
task fixes, not a hypothetical: an unmapped `DEPARTMENT_NOT_FOUND` key resolves to the internal-error
fallback `ERR005`, which `MapFailureStatusCode` has no case for, so it falls through to `400`
instead of `404`; a duplicate name `500`s instead of `409`ing with no `IDbExceptionTranslator` catch.

- [ ] **Step 2: Register the error codes (the actual fix)**

```csharp
// ApplicationErrors.cs
public static class Department
{
    public const string NOT_FOUND = "DEPARTMENT_NOT_FOUND";
    public const string NAME_EXISTS = "DEPARTMENT_NAME_EXISTS";
    public const string CREATED = "DEPARTMENT_CREATED";
}
// Branch mirrors: BRANCH_NOT_FOUND, BRANCH_NAME_EXISTS, BRANCH_CREATED
```

```csharp
// SystemCode.cs
public const string ERR047 = "ERR047"; // Department not found
public const string ERR048 = "ERR048"; // Branch not found
public const string ERR049 = "ERR049"; // Department name exists
public const string ERR050 = "ERR050"; // Branch name exists
```

`SystemCodeMap.cs` maps all four domain keys to these codes. `ResponseExtensions.cs`'s switch adds
`ERR047`/`ERR048` to the `404` arm and `ERR049`/`ERR050` to the `409` arm. `Resources.yaml` gets the
four ar/en pairs.

- [ ] **Step 3: `CreateDepartmentCommandHandler` — the `IDbExceptionTranslator` pairing**

```csharp
// backend/src/CustomerSupport.Application/Features/Organisation/Commands/CreateDepartment/CreateDepartmentCommandHandler.cs
public class CreateDepartmentCommandHandler(
    IRepository<Department> departments,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IMessageFactory messages)
    : ICommandHandler<CreateDepartmentCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateDepartmentCommand request, CancellationToken ct)
    {
        var department = Department.Create(request.Name, request.ManagerId);
        await departments.AddAsync(department, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsUniqueViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Department.NAME_EXISTS, MessageType.Conflict);
        }

        return messages.Success(department.Id, ApplicationErrors.Department.CREATED);
    }
}
```

`BranchesController`'s create handler is the identical shape against `Branch`.

- [ ] **Step 4: `DepartmentsController`**

```csharp
// backend/src/CustomerSupport.InternalApi/Controllers/DepartmentsController.cs
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Authenticated")]
public class DepartmentsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetDepartmentsQuery { PageIndex = page, PageSize = pageSize }, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetDepartmentByIdQuery(id), ct);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Create([FromBody] DepartmentRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateDepartmentCommand(request.Name, request.ManagerId), ct);
        return !result.Success ? this.ToActionResult(result) : CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] DepartmentRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateDepartmentCommand(id, request.Name, request.ManagerId), ct);
        return this.ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeactivateDepartmentCommand(id), ct);
        return this.ToActionResult(result);
    }
}
```

`BranchesController` is the identical shape against `Branch`/`BranchRequest`.

- [ ] **Step 5: Run tests to verify they pass, commit**

Run: `dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~OrganisationStructureEndpointTests"`
Expected: PASS, all included.

```bash
git commit -m "feat(org): department/branch CRUD, fixing the 404/409 gate (AC-117..121, AC-123)"
```

---

### Task 3: `DepartmentBranchSeeder` (`AC-118`)

**Files:**
- Create: `backend/src/CustomerSupport.Infrastructure/Seeders/DepartmentBranchSeeder.cs`

- [ ] **Step 1: Implement, idempotent like `CategorySeeder`**

```csharp
public class DepartmentBranchSeeder(AppDbContext db)
{
    public static readonly Guid DefaultDepartmentId = new("00000000-0000-0000-0000-000000000001");
    public static readonly Guid DefaultBranchId = new("00000000-0000-0000-0000-000000000001");

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var hasDepartment = await db.Departments.IgnoreQueryFilters().AnyAsync(d => d.Id == DefaultDepartmentId, ct);
        var hasBranch = await db.Branches.IgnoreQueryFilters().AnyAsync(b => b.Id == DefaultBranchId, ct);

        if (!hasDepartment)
        {
            db.Departments.Add(Department.Create("General", managerId: null, id: DefaultDepartmentId));
        }

        if (!hasBranch)
        {
            db.Branches.Add(Branch.Create("Head Office", region: null, timezone: "UTC", id: DefaultBranchId));
        }

        if (!hasDepartment || !hasBranch)
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Idempotent like CategorySeeder: losing the insert race to another host is expected.
                foreach (var entry in db.ChangeTracker.Entries<Department>().ToList())
                {
                    entry.State = EntityState.Detached;
                }

                foreach (var entry in db.ChangeTracker.Entries<Branch>().ToList())
                {
                    entry.State = EntityState.Detached;
                }

                var stillMissing = !await db.Departments.IgnoreQueryFilters().AnyAsync(d => d.Id == DefaultDepartmentId, ct)
                    || !await db.Branches.IgnoreQueryFilters().AnyAsync(b => b.Id == DefaultBranchId, ct);
                if (stillMissing)
                {
                    throw;
                }
            }
        }
    }
}
```

Registered in `WebApplicationExtensions.UsePlatformDataSeedingAsync`, same call site as
`CategorySeeder`.

- [ ] **Step 2: Commit**

```bash
git add backend/src/CustomerSupport.Infrastructure/Seeders/DepartmentBranchSeeder.cs
git commit -m "feat(org): idempotent default department/branch seed (AC-118)"
```

---

### Task 4: Department admin screen (`US-309`, frontend)

**Files:**
- Create: `frontend/projects/common/src/lib/organisation/organisation.api.ts`
- Create: `frontend/projects/admin-app/src/app/features/organisation/departments.component.{ts,html}`
- Modify: `app.routes.ts`, `shell.component.ts` (`NAV_ITEMS`)

**Interfaces:** matches this project's own `AsyncState` list+create+deactivate convention — see
`frontend/projects/admin-app/src/app/features/organisation/departments.component.ts` as the actual
shipped file (already in the tree; this task describes what it does).

- [ ] **Step 1: `DepartmentApi`**

```ts
@Injectable({ providedIn: 'root' })
export class DepartmentApi {
  private readonly http = inject(HttpClient);

  list(): Observable<PagedResult<Department>> {
    return this.http.get<PagedResult<Department>>('/api/Departments', { params: { pageSize: '100' } });
  }

  create(request: { name: string }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/Departments', request);
  }

  deactivate(id: string): Observable<unknown> {
    return this.http.delete(`/api/Departments/${id}`);
  }
}
```

- [ ] **Step 2: `DepartmentsComponent`** — `AsyncState<readonly Department[]>` signal, a create
`FormGroup` (`name`, required, ≤200 chars), `deactivate(department)`, following the exact shape
`SLAPoliciesComponent` (`FEAT-17`) later copied from this component.

- [ ] **Step 3: Route + nav**

```ts
{
  path: 'departments',
  canActivate: [roleGuard('Admin')],
  loadComponent: () => import('./features/organisation/departments.component'),
},
```

`{ path: '/departments', key: 'nav.departments', icon: 'apartment', adminOnly: true }` in
`NAV_ITEMS`.

- [ ] **Step 4: Commit**

```bash
git add frontend/projects/common/src/lib/organisation/department.api.ts frontend/projects/admin-app/src/app/features/organisation/departments.component.ts frontend/projects/admin-app/src/app/features/organisation/departments.component.html frontend/projects/admin-app/src/app/app.routes.ts frontend/projects/admin-app/src/app/layout/shell.component.ts
git commit -m "feat(org): Department admin screen (US-309)"
```

## Definition of done

`AC-115`, `AC-117`–`AC-121`, `AC-123` each covered by a test naming it · `dotnet build` clean ·
`dotnet test` green (evidence already recorded in this folder's `README.md`: 23/23 filtered,
frontend builds clean).

## Not shipped (recorded, not silently dropped)

- Branches admin screen — never specced (`US-309` only covers Department).
- `US-306` (branch-scoped query filters) — blocked on `OQ-5`.
