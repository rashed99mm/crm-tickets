# Task 1 — Record a customer

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — US-001, tasks 1.1–1.3 |
| Feature | `FEAT-03` Customer records |
| Criteria | `AC-7`, `AC-8` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommand.cs`
- `src/CustomerSupport.Application/Features/Customers/Validators/CustomerValidators.cs`
- `src/CustomerSupport.Application/Features/Customers/Dtos/CustomerDtos.cs`
- `src/CustomerSupport.InternalApi/Controllers/CustomersController.cs`
- `tests/CustomerSupport.Tests/Integration/CustomerEndpointTests.cs`

## Test evidence

- `AC7_CreateCustomer_ValidRequest_Returns201WithLocation` — 201, `Location` header present, id in `data`
- `AC8_CreateCustomer_InvalidFields_Returns400KeyedByField` — `Name`, `Email` and `Phone` all reported in **one** response
- `AC8_CreateCustomer_NameOverLengthLimit_Returns400KeyedToName`

Against real LocalDB, not the in-memory provider. Suite: **193 passed, 0 failed.**

## Deviations from the plan

**1. The shared validator helper was written, then thrown away.**
`CustomerRules.ApplyTo(validator, x => x.Name, …)` was the first attempt, to avoid duplicating the
create rules in the update validator. It is silently wrong: FluentValidation derives
`ValidationFailure.PropertyName` from the member *expression*, and a rule built over an invoked
`Func` has no member expression to read. Every error would have arrived with an empty field key,
which destroys `AC-8` while still passing a naive "did it return 400" test.

Rewritten against the properties directly, duplicating about fifteen lines between the two
validators. The duplication is the correct trade: **the field key is the criterion.**

**2. `CreatedAtAction`, not `ToActionResult`.**
`AC-7` wants a `Location` header, and the shared `ToActionResult` helper's 201 branch does not emit
one. The controller branches on failure through the helper and builds the success itself.

## The point of this task

The entity validates and so does FluentValidation, and that is not redundancy. The validator produces
a field-keyed 400 a form can bind to; the entity guarantees the invariant for every other caller —
seeders, future handlers, tests. Deleting either one leaves a real hole, and deviation 1 above is
what happens when the validator half is optimised without understanding what it is for.
