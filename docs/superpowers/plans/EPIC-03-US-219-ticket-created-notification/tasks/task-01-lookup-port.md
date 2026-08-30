# Task 01 — Customer → login-user lookup port

**Criteria:** `AC-N1`, `AC-N5`

## Files

- `Application/Interfaces/IIdentityUserService.cs` — add `FindByCustomerIdAsync`.
- `Infrastructure/Services/IdentityUserService.cs` — implement it.
- `backend/tests/CustomerSupport.Tests/Integration/` — integration test asserting the lookup.

## Steps (TDD — failing test first)

1. Write a failing integration test in the `CrmApiFactory` style (real LocalDB): create a user,
   set `user.LinkCustomer(customerId)`, save, call `IIdentityUserService.FindByCustomerIdAsync(customerId)`,
   assert it returns the user; assert a customer with no linked user returns `null`.
2. Add `Task<ApplicationUser?> FindByCustomerIdAsync(Guid customerId, CancellationToken ct = default)`
   to `IIdentityUserService`.
3. Implement in `IdentityUserService` (it already has `_dbContext`, `Infrastructure/Services/IdentityUserService.cs:16`):

   ```csharp
   public Task<ApplicationUser?> FindByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
       => _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.CustomerId == customerId, ct);
   ```

   Null `CustomerId` never matches, so a staff/unlinked customer yields `null` — this is the `AC-N5`
   input.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~FindByCustomerId"`

**Commit:** `feat: resolve customer to linked portal user for notifications`

**Deviation log:** (fill after execution)
