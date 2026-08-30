# Task 2 — A duplicate email is a conflict, not a validation error

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — US-116, tasks 2.1–2.3 |
| Feature | `FEAT-03` Customer records |
| Criteria | `AC-9`, and `AC-16`'s reuse half |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommand.cs` (`UniqueViolation`)
- `src/CustomerSupport.Domain/Entities/Customers/Customer.cs` (normalisation)
- `tests/CustomerSupport.Tests/Integration/CustomerEndpointTests.cs`
- `tests/CustomerSupport.Tests/Unit/Domain/CustomerTests.cs`

## Test evidence

- `AC9_CreateCustomer_DuplicateEmail_Returns409NotValidationError` — 409 with `CUSTOMER_EMAIL_EXISTS`
- `AC9_CreateCustomer_DuplicateEmailDifferentCase_Returns409`
- `AC9_Email_Is_Lowercased_So_The_Unique_Index_Catches_Case_Variants` (unit)
- `AC16_CreateCustomer_EmailOfDeletedCustomer_Succeeds` — the filtered index doing its job

Suite: **193 passed, 0 failed.**

## Deviations from the plan

**1. The unique-violation catch is matched by SQL error number, through reflection.**
The plan said "a `DbUpdateException` on the index must also surface as 409" without saying how.
Catching `SqlException` by type would mean the **Application** layer referencing
`Microsoft.Data.SqlClient` — a database provider in the wrong project, and a dependency-rule
violation of exactly the kind `CLAUDE.md` says must not bend.

`UniqueViolation.WasHit` walks the inner-exception chain and reads a `Number` property reflectively,
matching 2601 and 2627. Matched on the numbers rather than the message text, which SQL Server
localises by its own language setting and which would silently stop matching on a non-English
instance.

Reflection is not free — it is untyped and would not fail at compile time if the shape changed. The
alternative was worse.

## The point of this task

The check and the index do different jobs, and the plan says so: **the check exists to produce a good
message, the index exists to produce correctness.** `ExistsAsync` then insert is not atomic, so two
concurrent creates can both pass the check. If losing that race produced a 500, the criterion would
hold only under low load — which is the same as not holding.

`AC16_CreateCustomer_EmailOfDeletedCustomer_Succeeds` is the other half and the reason the index is
filtered on `IsDeleted = 0`. A plain unique index would refuse that create, and the conflict would
point at a record the user can no longer see — a support call nobody can resolve from the UI.
