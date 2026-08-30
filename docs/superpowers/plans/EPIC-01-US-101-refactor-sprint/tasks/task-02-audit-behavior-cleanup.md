# T2 — Remove unused EF Core import from AuditBehavior

**AC:** AC-R10
**Status:** done — `AuditBehavior.cs` carries no `Microsoft.EntityFrameworkCore` import.

## What this task does

Removes the `using Microsoft.EntityFrameworkCore;` line from `AuditBehavior.cs` which is never used in the file body.

## Files to modify

- `backend/src/CustomerSupport.Application/Behaviors/AuditBehavior.cs` — remove line 5

## Verification

`dotnet build` succeeds with no missing reference.
