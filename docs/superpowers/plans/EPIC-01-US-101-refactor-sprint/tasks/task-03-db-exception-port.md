# T3 — Add IDbExceptionTranslator port + Infrastructure implementation

**AC:** AC-R2
**Status:** done — `IDbExceptionTranslator` exists in `Application/Interfaces`; `DbExceptionTranslator` in `Infrastructure/Services` implements it and catches `DbUpdateException`/`DbUpdateConcurrencyException`.

## What this task does

Creates a port interface `IDbExceptionTranslator` in Application that lets handlers detect persistence-specific failure modes without importing EF Core types. Infrastructure provides the implementation that catches `DbUpdateException` and `DbUpdateConcurrencyException`.

Also moves the `UniqueViolation` helper (currently in `CreateCustomerCommand.cs`) into the Infrastructure implementation.

## Files to create

- `backend/src/CustomerSupport.Application/Interfaces/IDbExceptionTranslator.cs`
- `backend/src/CustomerSupport.Infrastructure/Services/DbExceptionTranslator.cs`

## Files to modify

- `backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs` — register `IDbExceptionTranslator`

## Verification

`dotnet build` succeeds. No EF Core types in Application layer.
