# FEAT-03 — Customer records · task record

**Plan:** [`implementation-plan/implementation-plan.md`](./implementation-plan.md)
**Executed:** 2026-08-25 → 2026-08-26
**Status:** delivered, API-only as planned

## Evidence

```
dotnet test CustomerSupport.slnx
Passed!  - Failed: 0, Passed: 193, Skipped: 0, Total: 193, Duration: 28 s
```

Backend build: 0 errors. 8 warnings, all pre-existing and all in inherited files
(`ContentSpecifications`, `ExternalApiServiceCollectionExtensions`, `ServiceCollectionExtensions`,
`GetExternalApiConfigurationsQuery`) — none in code this feature added.

## Tasks

| # | Task | Criteria | Commit | Status |
|---|---|---|---|---|
| [01](./tasks/task-01-validation-status-code.md) | Validation answers 400, not the inherited 422 | AC-8, AC-11, AC-30, AC-31, AC-51 | uncommitted | `done` |
| [02](./tasks/task-02-create-customer.md) | Record a customer | AC-7, AC-8 | uncommitted | `done` |
| [03](./tasks/task-03-duplicate-email-conflict.md) | A duplicate email is a conflict, not a validation error | AC-9, AC-16 | uncommitted | `done` |
| [04](./tasks/task-04-find-customers.md) | Find a customer | AC-10, AC-11, AC-13 | uncommitted | `done` |
| [05](./tasks/task-05-read-and-correct.md) | Read and correct a customer | AC-12, AC-14 | uncommitted | `done` |
| [06](./tasks/task-06-delete-guard.md) | The delete guard | AC-15, AC-16, AC-12 | uncommitted | `done` |

## Criteria delivered

| `AC-n` | Test naming it | Where |
|---|---|---|
| AC-7 | `AC7_CreateCustomer_ValidRequest_Returns201WithLocation` | `CustomerEndpointTests` |
| AC-8 | `AC8_CreateCustomer_InvalidFields_Returns400KeyedByField`, `AC8_CreateCustomer_NameOverLengthLimit_Returns400KeyedToName` | " |
| AC-9 | `AC9_CreateCustomer_DuplicateEmail_Returns409NotValidationError`, `AC9_CreateCustomer_DuplicateEmailDifferentCase_Returns409`, plus `AC9_Email_Is_Lowercased_So_The_Unique_Index_Catches_Case_Variants` (unit) | " + `CustomerTests` |
| AC-10 | `AC10_GetCustomers_ReturnsPagedEnvelope` | `CustomerEndpointTests` |
| AC-11 | `AC11_GetCustomers_PageSizeAboveMaximum_Returns400` | " |
| AC-12 | `AC12_GetCustomer_UnknownId_Returns404`, `AC12_UpdateCustomer_UnknownId_Returns404`, `AC12_DeleteCustomer_UnknownId_Returns404` | " |
| AC-13 | `AC13_GetCustomers_SearchTerm_MatchesNameOrEmail` | " |
| AC-14 | `AC14_UpdateCustomer_ValidChange_Persists`, `AC14_UpdateCustomer_EmailTakenByAnother_Returns409`, `AC14_UpdateCustomer_InvalidEmail_Returns400` | " |
| AC-15 | `AC15_DeleteCustomer_WithTickets_Returns409AndCustomerRemains` | **`TicketEndpointTests`** — see D3 |
| AC-16 | `AC16_DeleteCustomer_WithoutTickets_Returns200AndDisappearsFromList`, `AC16_CreateCustomer_EmailOfDeletedCustomer_Succeeds` | `CustomerEndpointTests` |
| AC-3 | `AC3_Customers_WithoutAToken_Returns401` | " |

## Deviations from the plan

**D1 — Field keys are PascalCase on the wire, not camelCase.**
The plan and the first draft of the tests assumed `errors[]` would be keyed `name`, `email`,
`pageSize`. FluentValidation reports `ValidationFailure.PropertyName` as the member name — `Name`,
`Email`, `PageSize` — and `ValidationBehavior` groups on exactly that. The test assertions were
corrected to the real contract rather than the contract being bent to the tests, because the Angular
envelope interceptor **already** lowercases the first character on the way in (documented there as
`F3`). Changing the backend would have broken a mapping that was written, tested and working.

**D2 — The shared-rules helper was abandoned mid-task.**
`CustomerRules.ApplyTo(validator, x => x.Name, …)` was written first, to avoid duplicating the create
rules in the update validator. It is wrong: FluentValidation derives `PropertyName` from the member
*expression*, and a rule built over an invoked `Func` has no member expression to read — so every
error would have arrived with an empty field key, silently destroying AC-8. The validators were
rewritten against the properties directly, duplicating about fifteen lines. The duplication is the
correct trade: the field key **is** the criterion.

**D3 — `AC-15` is tested in `TicketEndpointTests`, not `CustomerEndpointTests`.**
Its precondition is a ticket, so it cannot run before `FEAT-04` exists. The plan predicted this and
it is why the two features share a sprint; the test simply lives with the fixture that can satisfy
it. Named `AC15_…` so the traceability search still finds it.

**D4 — `AC-10`'s envelope field is `pageIndex`, not `page`.**
`AC-10` names `items`, `page`, `pageSize`, `totalCount`. The inherited `PaginatedList<T>` serialises
`pageIndex`, and it is already the shape the users list and its frontend consume. Kept `pageIndex`
rather than renaming a type six existing features depend on. **This is a real, if small, divergence
from the criterion's literal text** and is recorded here rather than glossed.

The frontend's stale `PagedResult<T>` (which declares `page`) was **not** corrected either — a
correctly-shaped `TicketPage` was declared alongside it instead, leaving the divergence documented
in two places rather than fixed in one. See `D1` in the FEAT-05 frontend record. Both are flagged
for FEAT-09's contract-hardening pass.

## Accepted risks

**`AC-13`'s case-insensitivity is the database's, not the code's.** `Contains` maps to `LIKE`, and
SQL Server's default collation is case-insensitive. Nothing in the handler enforces it. The test
asserts the behaviour so a collation change fails loudly instead of quietly narrowing every search.

**The duplicate-email check is a race.** `ExistsAsync` then insert is not atomic; two concurrent
creates can both pass the check. `UX_Customers_Email` settles it, and the handler catches the
resulting `DbUpdateException` and returns the same 409. The check exists for the *message*, the index
for the *correctness*. The unique-violation detection matches SQL Server error numbers 2601/2627
through reflection rather than catching `SqlException` by type, which would put a database provider
reference in the Application layer.
