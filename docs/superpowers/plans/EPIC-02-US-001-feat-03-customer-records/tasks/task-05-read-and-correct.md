# Task 4 — Read and correct a customer

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — US-002, tasks 4.1–4.3 |
| Feature | `FEAT-03` Customer records |
| Criteria | `AC-12`, `AC-14` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Customers/Queries/GetCustomerById/GetCustomerByIdQuery.cs`
- `src/CustomerSupport.Application/Features/Customers/Commands/UpdateCustomer/UpdateCustomerCommand.cs`
- `src/CustomerSupport.Domain/Entities/Customers/Customer.cs` (`Update`)
- `tests/CustomerSupport.Tests/Integration/CustomerEndpointTests.cs`

## Test evidence

- `AC12_GetCustomer_UnknownId_Returns404`
- `AC12_UpdateCustomer_UnknownId_Returns404`
- `AC12_DeleteCustomer_UnknownId_Returns404`
- `AC14_UpdateCustomer_ValidChange_Persists` — re-fetches to prove persistence, not just the 200
- `AC14_UpdateCustomer_EmailTakenByAnother_Returns409`
- `AC14_UpdateCustomer_InvalidEmail_Returns400`

Suite: **193 passed, 0 failed.**

## Deviations from the plan

**1. `AC12_UpdateCustomer_UnknownId_Returns404` was missing and added late.**
The plan's task 4.1 covered the fetch path, and tests existed for fetch and delete. `AC-12` names
"fetching, updating **or** deleting" — the update third was implemented but unproven until the
test-case table was reconciled. Found by reading `US-002`'s TC-02 row, not by reading the code, which
is the argument for keeping those tables honest rather than blanket-marking them done.

**2. The uniqueness check excludes the row being updated.**
Not in the plan, and necessary: `ExistsAsync(c => c.Email == normalised)` alone would reject
re-saving a customer with its own email unchanged, which is an ordinary update rather than a
conflict. The predicate carries `&& c.Id != request.Id`.

## The point of this task

**Soft-deleted customers answer 404 for free.** `GetByIdAsync` runs through the global
`IsDeleted == false` query filter that `AppDbContext` applies to every `BaseEntity`, so "deleted" and
"never existed" are indistinguishable to a caller — which is exactly what `AC-12` and `AC-16` want
together. Nothing in the handler checks `IsDeleted`, and nothing should; the tests pin the behaviour
so that removing the filter cannot pass silently.

`AC-14` says validation matches creation's, so `UpdateCustomerCommandValidator` restates the same
rules rather than defining a laxer second set. `Customer.Update` calls the same private `Validate`
the factory does, so the entity cannot be moved into a state the factory would have refused.
