# T5 — Remove IQueryable from IRepository; add query methods

**AC:** AC-R3
**Status:** done — `IRepository<T>` exposes `ListAsync`, `ListOrderedAsync`, `ListProjectedAsync`, `ListProjectedOrderedAsync`, `GetPagedAsync`; no `Query`/`QueryInclude`/`IQueryable<T>` anywhere in the interface.

## What this task does

Removes `IQueryable<T> Query(...)` and `IQueryable<T> QueryInclude(...)` from `IRepository<T>`. Replaces them with purpose-built methods:

- `Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate, CancellationToken ct)` — filtered list without pagination
- `Task<IReadOnlyList<T>> ListAsync<TOrderKey>(Expression<Func<T, bool>>? predicate, Expression<Func<T, TOrderKey>> orderBy, bool descending, CancellationToken ct)` — filtered + ordered list
- `Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct)` — already exists

The `GetPagedAsync` methods already exist and return `PaginatedList<TDto>` — those stay.

Also updates the Infrastructure repository implementation to provide the new methods using EF Core internally.

## Files to modify

- `backend/src/CustomerSupport.Domain/Interfaces/IRepository.cs` — remove `Query`, `QueryInclude`
- `backend/src/CustomerSupport.Infrastructure/Persistence/Repository.cs` (or equivalent) — add implementations

## Verification

`dotnet build` succeeds with no references to removed methods.
