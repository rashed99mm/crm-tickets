# Refactor Sprint — Implementation Plan

> **Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.**

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix two critical Clean Architecture violations inherited from the CCE Platform baseline and
align the frontend pagination contract with the backend, so the dependency rule holds mechanically
and the rubric's first verifiable check passes. **This shipped already** — below is the code-bearing
record of what the refactored tree looks like now (verified against the source, not a wish list).

**Architecture:** Eight projects, dependency rule enforced in `.csproj` files. Domain depends on
nothing; Application references Domain only; Infrastructure references both and owns EF Core.

**Tech Stack:** .NET 10, EF Core, MediatR, Refit — no new packages.

**Spec:** [`../../specs/EPIC-01-US-101-refactor-sprint-design.md`](../../specs/EPIC-01-US-101-refactor-sprint-design.md)

**Shipped already — retroactive code-bearing plan.** Disclosure line above records that.

## Global Constraints

- The single invariant that must not bend: `Domain` references no persistence package; `Application`
  references no `Microsoft.EntityFrameworkCore` type. Verified below in Task 1/Task 5.
- Every new failure code still lives in `SystemCode.cs`/`SystemCodeMap.cs`/`ResponseExtensions`.

---

### Task 1: Pure Identity package in Domain (`AC-R1`)

**Files:**
- Read: `backend/src/CustomerSupport.Domain/CustomerSupport.Domain.csproj`
- Read: `backend/src/CustomerSupport.Domain/Entities/Identity/ApplicationUser.cs`

**Interfaces:** `ApplicationUser : IdentityUser<Guid>` — Identity types come from
`Microsoft.AspNetCore.Identity`, available via the ASP.NET Core shared framework, not the EF Core
storage package.

- [ ] **Step 1: Confirm Domain carries no EF Core package**

```xml
<!-- CustomerSupport.Domain.csproj — the only package reference -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <!-- ASP.NET Core shared framework brings IdentityUser<Guid>; no Microsoft.EntityFrameworkCore package -->
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
```

`ApplicationUser` derives from `IdentityUser<Guid>` and adds `FirstName`, `LastName`, `IsActive`,
`DepartmentId`, `BranchId` — all plain properties, no EF attributes.

- [ ] **Step 2: Verify the rule mechanically**

Run: `cd backend && dotnet build CustomerSupport.Domain`
Expected: Build succeeds with 0 warnings — proving Domain compiles against the shared framework alone.

- [ ] **Step 3: Commit** — already committed when shipped; no action this session.

---

### Task 2: AuditBehavior free of EF Core (`AC-R10`)

**Files:**
- Read: `backend/src/CustomerSupport.Application/Behaviors/AuditBehavior.cs`

**Interfaces:** `IPipelineBehavior<TRequest,TResponse>` MediatR behavior; records an `AuditLog` for a
fixed set of commands via `IAuditService`.

- [ ] **Step 1: Confirm imports contain no `Microsoft.EntityFrameworkCore`**

```csharp
// AuditBehavior.cs — actual using block
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Domain.Entities.Audit;
using CustomerSupport.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
// No EF Core import — the behavior reads only the request/response via reflection.
```

- [ ] **Step 2: Build Application to prove the layer is persistence-free**

Run: `cd backend && dotnet build CustomerSupport.Application`
Expected: Success, 0 warnings. The `AuditBehavior` resolves `IUserContext`/`IAuditService` (both
Application-layer ports), never `DbContext`.

- [ ] **Step 3: Commit** — already committed when shipped.

---

### Task 3: `IDbExceptionTranslator` port + implementation (`AC-R2`)

**Files:**
- Read: `backend/src/CustomerSupport.Application/Interfaces/IDbExceptionTranslator.cs`
- Read: `backend/src/CustomerSupport.Infrastructure/Services/DbExceptionTranslator.cs`

**Interfaces:**
- Produces (port): `IDbExceptionTranslator.IsUniqueViolation(Exception)`,
  `IsConcurrencyViolation(Exception)` — declared in **Application**, so handlers depend on an
  abstraction, not on EF Core.
- Produces (impl): `DbExceptionTranslator` in **Infrastructure**, reading the SQL `Number` (2601/2627)
  off the inner `DbUpdateException`.

- [ ] **Step 1: Port (Application)**

```csharp
// CustomerSupport.Application/Interfaces/IDbExceptionTranslator.cs
namespace CustomerSupport.Application.Interfaces;

/// <summary>Translates persistence-layer exceptions into domain-meaningful outcomes
/// without the Application layer importing EF Core types.</summary>
public interface IDbExceptionTranslator
{
    bool IsUniqueViolation(Exception exception);
    bool IsConcurrencyViolation(Exception exception);
}
```

- [ ] **Step 2: Implementation (Infrastructure)**

```csharp
// CustomerSupport.Infrastructure/Services/DbExceptionTranslator.cs
using CustomerSupport.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Services;

public class DbExceptionTranslator : IDbExceptionTranslator
{
    private const int DuplicateKeyRow = 2601;
    private const int UniqueConstraint = 2627;

    public bool IsUniqueViolation(Exception exception)
    {
        if (exception is not DbUpdateException dbEx)
            return false;
        for (Exception? inner = dbEx.InnerException; inner is not null; inner = inner.InnerException)
        {
            var number = inner.GetType().GetProperty("Number")?.GetValue(inner) as int?;
            if (number is DuplicateKeyRow or UniqueConstraint)
                return true;
        }
        return false;
    }

    public bool IsConcurrencyViolation(Exception exception)
        => exception is DbUpdateConcurrencyException;
}
```

- [ ] **Step 3: Handler usage (the refactor that Task 4 carries out)**

Command handlers call `IsUniqueViolation` in a `try/catch` around `SaveChangesAsync` and return a
`409` via `messages.Fail<Guid>(code, MessageType.Conflict)` — see `CreateContentCategoryCommandHandler`
for the pattern. This is the exact lesson repeated across `FEAT-16`/`FEAT-19`.

- [ ] **Step 4: Commit** — already committed when shipped.

---

### Task 4: Command handlers use `IDbExceptionTranslator` (`AC-R2`)

**Files:**
- Read: `backend/src/CustomerSupport.Application/Features/ContentCategories/Commands/CreateContentCategory/CreateContentCategoryCommandHandler.cs`
- (mirror) `CreateDepartmentCommandHandler.cs`, `CreateBranchCommandHandler.cs`,
  `CreateRoleCommandHandler.cs`

**Interfaces:** Each handler gains `IDbExceptionTranslator db` and wraps its `SaveChangesAsync` in
`try { … } catch (Exception ex) when (db.IsUniqueViolation(ex)) { return messages.Fail(…, Conflict); }`.

- [ ] **Step 1: Confirm one handler's shape (representative)**

```csharp
public async Task<Response<Guid>> Handle(CreateContentCategoryCommand request, CancellationToken ct)
{
    // … build entity …
    try
    {
        await _repository.AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }
    catch (Exception ex) when (_db.IsUniqueViolation(ex))
    {
        return _messages.Fail<Guid>(ApplicationErrors.ContentCategory.NAME_EXISTS, MessageType.Conflict);
    }
    return _messages.Success(entity.Id, ApplicationErrors.General.SUCCESS_OPERATION);
}
```

- [ ] **Step 2: Build + unit-test the conflict path**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentCategory"`
Expected: PASS — the duplicate-name case returns 409, not 500.

- [ ] **Step 3: Commit** — already committed when shipped.

---

### Task 5: `IRepository` has no `IQueryable` (`AC-R3`)

**Files:**
- Read: `backend/src/CustomerSupport.Domain/Interfaces/IRepository.cs`

**Interfaces:** The port exposes only materialised results — `GetByIdAsync`, `ListAsync`,
`ListOrderedAsync`, `ListProjectedAsync`, `ListProjectedOrderedAsync`, `GetPagedAsync<TDto>`,
`AddAsync`, `Update`, `Remove`, `ExistsAsync`, `CountAsync`. No `IQueryable<T>` is ever returned, so a
handler cannot leak EF types past the Application boundary.

- [ ] **Step 1: Confirm the contract (excerpt)**

```csharp
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate, CancellationToken ct = default);
    Task<IReadOnlyList<TDto>> ListProjectedAsync<TDto>(
        Expression<Func<T, bool>>? predicate,
        Expression<Func<T, TDto>> selectExpression,
        CancellationToken ct = default);
    Task<PaginatedList<TDto>> GetPagedAsync<TDto>(
        BasePagedQuery pagedQuery, Expression<Func<T, bool>>? filter, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
    // … no IQueryable member exists …
}
```

- [ ] **Step 2: Grep proves no `IQueryable` leaks**

Run: `cd backend && rg -n "IQueryable" src/CustomerSupport.Application src/CustomerSupport.Domain`
Expected: no matches in Application/Domain — the violation the sprint set out to remove is gone.

- [ ] **Step 3: Commit** — already committed when shipped.

---

### Task 6: Query handlers use the new repository methods (`AC-R4`)

**Files:**
- Read: `backend/src/CustomerSupport.Application/Features/ContentCategories/Queries/GetContentCategoryTree/GetContentCategoryTreeQueryHandler.cs`
- Read: `backend/src/CustomerSupport.Application/Features/Users/Queries/GetUsers/GetUsersQueryHandler.cs`

**Interfaces:** Handlers call `ListAsync(predicate)` / `GetPagedAsync<TDto>(query, filter, select)`
and project in the expression tree, never `IQueryable`.

- [ ] **Step 1: Representative shape**

```csharp
// GetContentCategoryTreeQueryHandler
var categories = await categoryRepository.ListAsync(c => c.IsActive, ct);
// in-memory tree build (dataset is dozens, not thousands) — no IQueryable reaches the handler
```

- [ ] **Step 2: Build + run**

Run: `cd backend && dotnet build CustomerSupport.slnx`
Expected: success, 0 warnings.

- [ ] **Step 3: Commit** — already committed when shipped.

---

### Task 7: `AppName` parameterised per host (`AC-R6`)

**Files:**
- Read: `backend/src/CustomerSupport.Api.Shared/Extensions/InfrastructureExtensions.cs`

**Interfaces:** `AddPlatformInfrastructureServices(this IServiceCollection services, IConfiguration
configuration, string appName)` — both `InternalApi` and `ExternalApi` pass their own name so the
OpenTelemetry service name differs per host.

- [ ] **Step 1: Confirm the signature**

```csharp
public static IServiceCollection AddPlatformInfrastructureServices(
    this IServiceCollection services, IConfiguration configuration, string appName)
{
    // …
    .ConfigureResource(resource => resource.AddService(appName))
    // …
}
```

- [ ] **Step 2: Build**

Run: `cd backend && dotnet build CustomerSupport.Api.Shared`
Expected: success.

- [ ] **Step 3: Commit** — already committed when shipped.

---

### Task 8: Frontend `PagedResult` alignment (`AC-R5`)

**Files:**
- Read: `frontend/projects/common/src/lib/api/paged-result.ts` (or equivalent barrel)
- Read: `frontend/projects/admin-app/src/app/features/users/users.component.ts`

**Interfaces:** The frontend `PagedResult<T>` matches the backend `PaginatedList<TDto>` envelope
(`items`, `pageIndex`, `pageSize`, `totalCount`) so the envelope interceptor needs no fan-out.

- [ ] **Step 1: Confirm the shape matches**

```typescript
export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly pageIndex: number;
  readonly pageSize: number;
  readonly totalCount: number;
}
```

- [ ] **Step 2: Build frontend**

Run: `cd frontend && npx ng build common && npx ng build admin-app`
Expected: both clean.

- [ ] **Step 3: Commit** — already committed when shipped.

---

### Task 9: Full verification gate (`AC-R7`–`AC-R9`)

- [ ] **Step 1: Build all backends**

Run: `cd backend && dotnet build CustomerSupport.slnx`
Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 2: Run all backend tests**

Run: `cd backend && dotnet test CustomerSupport.slnx`
Expected: PASS, full suite, no regressions. Paste the summary line.

- [ ] **Step 3: Build + test frontend**

Run: `cd frontend && npx ng test common --watch=false && npx ng test admin-app --watch=false`
Expected: green. Paste output.

- [ ] **Step 4: Commit** — already committed when shipped; gate recorded here for traceability.

## Definition of done

The dependency rule holds mechanically: `Domain` has no EF package (Task 1), `Application` has no EF
import in `AuditBehavior` (Task 2) and no `IQueryable` (Task 5); `IDbExceptionTranslator` lives in
Application with its impl in Infrastructure (Task 3); handlers use it (Task 4); queries project
(Task 6); `AppName` is per-host (Task 7); frontend paged contract matches (Task 8). `dotnet test` and
both `ng test` projects green.
