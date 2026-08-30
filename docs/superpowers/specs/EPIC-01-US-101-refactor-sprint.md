# Refactor Sprint — Clean Architecture Compliance & Contract Alignment

## Problem

The inherited CCE Platform baseline has two critical Clean Architecture violations that undermine the dependency rule — the single architectural invariant the rubric grades mechanically. The frontend also has a contract mismatch (`page` vs `pageIndex`) that creates duplicate type definitions across every paginated feature.

An assessor can mechanically verify the dependency rule by opening `Domain.csproj` and checking for persistence packages. Today that check fails. The Application layer also uses EF Core extension methods and exception types directly, which means use-case logic is coupled to a specific persistence implementation.

## Assumptions

A1. The Identity entity classes (`ApplicationUser`, `ApplicationRole`, and the five thin wrappers) must remain in the Domain layer because Domain entities reference `ApplicationUser` (Ticket.AssigneeId, Ticket.CreatedBy, CustomerNote.AuthorId, Asset.UploadedById) and `ApplicationRole.Roles.*` constants are used across Application handlers for authorization checks.
A2. Moving Identity entities out of Domain would cascade changes into every handler, EF configuration, seeder, and test — disproportionate for a refactor sprint whose goal is surgical compliance, not a rewrite.
A3. The `IRepository<T>.Query()` returning `IQueryable<T>` is the root enabler of EF Core coupling in Application handlers — removing it requires adding repository methods that encapsulate the query patterns currently expressed as LINQ-to-SQL.
A4. The frontend `PagedResult<T>` in `api-response.ts` declaring `page` is unused by any consumer — all five paginated interfaces already define their own `pageIndex`-based shape as a workaround.
A5. The `Api.Shared` `AppName` constant being hardcoded to `"CustomerSupport.InternalApi"` is inherited from the CCE Platform and was never parameterized when the two-host split happened (ADR-0008).

## Out of scope

- Rewriting the entire repository pattern or introducing a Unit of Work abstraction beyond what exists
- Moving Identity entities out of Domain (A1/A2)
- Implementing new features or new endpoints
- Portal-app feature implementation
- Replacing MediatR or changing the CQRS pattern
- Introducing new NuGet packages not already in `Directory.Packages.props`
- Changing the test infrastructure (WebApplicationFactory, LocalDB)

## Acceptance criteria

AC-R1. Given `Domain.csproj`, when inspected, then no `Microsoft.AspNetCore.Identity.EntityFrameworkCore` or `Microsoft.EntityFrameworkCore` package references exist in the project file.

AC-R2. Given Application handler files (`CreateCustomerCommand.cs`, `UpdateCustomerCommand.cs`, `AssignTicketCommand.cs`, `ChangeTicketStatusCommand.cs`, `GetTicketsQuery.cs`, `GetTicketByIdQuery.cs`, `GetCategoriesQuery.cs`, `GetCustomerAttachmentsQuery.cs`, `GetCustomerNotesQuery.cs`), when inspected, then no `using Microsoft.EntityFrameworkCore` import exists and no `DbUpdateException`, `DbUpdateConcurrencyException`, `FirstOrDefaultAsync`, `ToListAsync`, or `CountAsync` (EF Core extension methods) are called directly.

AC-R3. Given `IRepository<T>` in Domain, when inspected, then no `IQueryable<T>` return types exist. The interface exposes only `IReadOnlyList<T>`, `Task<T?>`, `Task<bool>`, `Task<int>`, `PaginatedList<T>`, and mutation methods.

AC-R4. Given Application query handlers (`GetTicketsQuery`, `GetTicketByIdQuery`, `GetCategoriesQuery`, `GetCustomerAttachmentsQuery`, `GetCustomerNotesQuery`), when executing a paginated or filtered query, then the correct data is returned using repository methods that do not expose `IQueryable<T>`.

AC-R5. Given the frontend `PagedResult<T>` in `api-response.ts`, when inspected, then the field name is `pageIndex` (matching the backend's `PaginatedList<T>.PageIndex` serialization), and the duplicate `TicketPage`, `CustomerPage`, `CustomerNotePage`, `CustomerAttachmentPage`, and `StaffUserList` interfaces are replaced by the canonical `PagedResult<T>`.

AC-R6. Given `InfrastructureExtensions.cs`, when both hosts start, then each reports its own application name in OpenTelemetry configuration (not both as `"CustomerSupport.InternalApi"`).

AC-R7. Given all backend changes, when running `cd backend && dotnet build CustomerSupport.slnx`, then the build succeeds with zero new warnings.

AC-R8. Given all changes, when running `cd backend && dotnet test CustomerSupport.slnx`, then all existing tests pass with no regressions.

AC-R9. Given all frontend changes, when running `cd frontend && npx ng build admin-app`, then the build succeeds.

AC-R10. Given `AuditBehavior.cs`, when inspected, then the unused `using Microsoft.EntityFrameworkCore;` import is removed.

## Design

### Backend: Domain layer

**Identity package removal (AC-R1):**

The `Microsoft.AspNetCore.Identity.EntityFrameworkCore` package in `Domain.csproj` exists solely because 6 entity files inherit from `IdentityUser<Guid>`, `IdentityRole<Guid>`, and the four `Identity*<Guid>` wrapper types. These base classes live in `Microsoft.AspNetCore.Identity` (the non-EF package), not `Microsoft.EntityFrameworkCore`.

The fix: replace the EF-specific package with `Microsoft.AspNetCore.Identity` (the pure identity package with no EF dependency). The base classes `IdentityUser<Guid>`, `IdentityRole<Guid>`, `IdentityUserClaim<Guid>`, etc. are all defined in `Microsoft.AspNetCore.Identity`, not in the EF package. The EF package only adds `IdentityDbContext` and EF-specific extensions.

**IQueryable removal (AC-R3):**

Replace `Query()` and `QueryInclude()` on `IRepository<T>` with purpose-built methods:
- `ListAsync(Expression<Func<T, bool>>?, CancellationToken)` → `IReadOnlyList<T>`
- `CountAsync(Expression<Func<T, bool>>?, CancellationToken)` → `int` (already exists)
- `GetPagedAsync` → already exists and returns `PaginatedList<T>` (already used by handlers)

The EF Core LINQ operations (`CountAsync`, `ToListAsync`, `FirstOrDefaultAsync`, `OrderByDescending().Skip().Take().Select().ToListAsync()`) currently called on `IQueryable<T>` in handlers will move into the repository implementation or into new repository methods.

**EF exception translation (AC-R2):**

Create `IDbExceptionTranslator` in Application that catches persistence-specific exceptions without the Application layer knowing about EF Core types. Infrastructure provides the implementation that catches `DbUpdateException` and `DbUpdateConcurrencyException`. Handlers call `translator.IsUniqueViolation(ex)` and `translator.IsConcurrencyViolation(ex)` instead of catching EF types directly.

### Backend: Application layer

**Handler refactoring (AC-R2, AC-R4):**

Each handler that currently calls `.Query().CountAsync()`, `.Query().ToListAsync()`, or `.Query().FirstOrDefaultAsync()` will use the new repository methods instead. The query composition (filtering, ordering, pagination) moves into repository methods that accept `Expression<Func<T, bool>>` predicates and return `IReadOnlyList<T>` or `PaginatedList<TDto>`.

### Backend: Api.Shared

**AppName parameterization (AC-R6):**

Replace the `private const string AppName` with a parameter passed from each host's composition root. InternalApi passes `"CustomerSupport.InternalApi"`, ExternalApi passes `"CustomerSupport.ExternalApi"`.

### Frontend

**PagedResult alignment (AC-R5):**

Fix the canonical `PagedResult<T>` in `api-response.ts` to use `pageIndex` instead of `page`. Then replace the five duplicate interface definitions (`TicketPage`, `CustomerPage`, `CustomerNotePage`, `CustomerAttachmentPage`, `StaffUserList`) with `PagedResult<T>` parameterized with their respective item types. Update all consumers of these local interfaces.

### Data model

No schema changes. No migrations. This is a pure refactoring of code organization, not behavior.

### Error behavior

No change to error contracts, status codes, or response shapes. The `IDbExceptionTranslator` preserves the same error-mapping behavior (unique violation → 409, concurrency → 409) by translating the same exceptions through a port interface.
