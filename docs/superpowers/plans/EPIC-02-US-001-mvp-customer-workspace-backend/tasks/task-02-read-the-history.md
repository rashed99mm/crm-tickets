# Task 2 — Read the history, newest first

| Field | Value |
|---|---|
| Plan | [`implementation-plan.md`](../implementation-plan.md) — T3, T4, T5 |
| Feature | `MVP-05` Interaction history (backend half) |
| Criteria | `AC-74`, and the inherited `AC-11` paging rule |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Customers/Queries/GetCustomerNotes/GetCustomerNotesQuery.cs` (new)
- `src/CustomerSupport.Application/Features/Customers/Validators/CustomerValidators.cs`
- `src/CustomerSupport.InternalApi/Controllers/CustomersController.cs`
- `tests/CustomerSupport.Tests/Integration/CustomerNotesEndpointTests.cs`

## Test evidence

- `AC74_GetNotes_ReturnsNewestFirstWithAuthorNames` — two notes, asserted in descending `createdAt`
  order **and** by body sequence, each carrying the author's resolved full name.
- `AC21_GetNotes_PageSizeAboveMaximum_Returns400` — `details` keyed to `PageSize`.

Against real LocalDB. Suite: **250 passed, 0 failed.**

## Deviations from the plan

**A 20 ms delay between the two writes in the ordering test.** `CreatedAt` is stamped from
`DateTime.UtcNow` inside `CustomerNote.Create`. Two consecutive requests can land inside one tick,
at which point the ordering assertion is asserting nothing and would pass against a handler that
sorted ascending. The delay makes the two stamps genuinely distinct.

Nothing else departs from the plan. Paging follows `GetTicketsQueryHandler` — count, then
skip/take, then project — and the author lookup follows `GetTicketByIdQueryHandler`.

## The point of this task

`authorName` is projected, never stored. `CustomerNote` holds `AuthorId` only, and the name is
resolved through `IIdentityUserService` at read time — **once per distinct author**, not once per
row. `ApplicationUser` is `IdentityUser<Guid>` and therefore outside `IRepository<T>`'s `BaseEntity`
constraint, so there is no queryable to join against from the Application layer; the port is the
only way through, and doing it per row would be an N+1 the moment a page held ten notes.

Storing the name instead would freeze a value that changes, in a table `A13` says nothing may ever
correct.

An unknown customer is a **404, not an empty page**. "This customer does not exist" and "this
customer has said nothing" are different facts and the detail screen has to distinguish them.
