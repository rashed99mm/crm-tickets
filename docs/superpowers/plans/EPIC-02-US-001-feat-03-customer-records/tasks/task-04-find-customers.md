# Task 3 — Find a customer

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — US-004, tasks 3.1–3.3 |
| Feature | `FEAT-03` Customer records |
| Criteria | `AC-10`, `AC-11`, `AC-13` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Customers/Queries/GetCustomers/GetCustomersQuery.cs`
- `src/CustomerSupport.Application/Features/Customers/Validators/CustomerValidators.cs` (`GetCustomersQueryValidator`)
- `tests/CustomerSupport.Tests/Integration/CustomerEndpointTests.cs`

## Test evidence

- `AC10_GetCustomers_ReturnsPagedEnvelope`
- `AC11_GetCustomers_PageSizeAboveMaximum_Returns400` — keyed to `PageSize`
- `AC13_GetCustomers_SearchTerm_MatchesNameOrEmail` — asserts the upper-cased term matches too
- `AC13_GetCustomers_SearchMatchingNothing_ReturnsEmptyPageNotAnError`

Suite: **193 passed, 0 failed.**

## Deviations from the plan

**1. The envelope field is `pageIndex`, not the `page` `AC-10` names.**
`AC-10` spells out `items`, `page`, `pageSize`, `totalCount`. The inherited `PaginatedList<T>`
serialises `pageIndex`, and it is already the shape the users list and its Angular consumer read.
Renaming it would have touched a type six existing features depend on, to satisfy the letter of one
criterion.

Kept `pageIndex`. **This is a real divergence from the criterion's literal text**, small but genuine,
and it is recorded here and in the feature README's `D4` rather than glossed. What was corrected
instead is the frontend's stale `PagedResult<T>` — see the FEAT-05 frontend record.

**2. The empty-search test was added after the fact.**
`US-004`'s TC-05 (a term matching nothing) had no test until the test-case table was reconciled
against reality. Added rather than marked uncovered, because it is three lines and it guards a real
failure: an empty result must be an empty page inside an intact envelope, never a 404, or the
frontend cannot tell "no matches" from "the request failed".

## The point of this task

**`AC-13`'s case-insensitivity is the database's, not the code's.** `Contains` compiles to `LIKE`,
and SQL Server's default collation is case-insensitive; nothing in the handler enforces it. That is
an accepted risk, not a guarantee, and the test asserts the behaviour precisely so a collation change
fails loudly instead of quietly narrowing every search an agent runs.

`AC-11` is as much a denial-of-service control as a correctness one — one request asking for every
row is all it takes, so the cap is 100 and exceeding it is a field-keyed 400.
