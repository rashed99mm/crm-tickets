# FEAT-04 — Ticket capture · backend task record

**Plan:** [`implementation-plan/implementation-plan.md`](./implementation-plan.md)
**Executed:** 2026-08-26
**Status:** delivered. Frontend counterpart written and implemented immediately after — see
[`../EPIC-02-US-016-feat-04-ticket-capture-frontend/`](../EPIC-02-US-016-feat-04-ticket-capture-frontend/)

## Evidence

```
dotnet test CustomerSupport.slnx
Passed!  - Failed: 0, Passed: 193, Skipped: 0, Total: 193, Duration: 28 s
```

## Tasks

| # | Task | Criteria | Commit | Status |
|---|---|---|---|---|
| [01](./tasks/task-01-category-seed.md) | Seed the fixed category list | A4, BR-14 | uncommitted | `done` |
| [02](./tasks/task-02-reference-generator.md) | Issue human-readable ticket references | AC-29, BR-15 | uncommitted | `done` |
| [03](./tasks/task-03-create-ticket.md) | Raise a ticket | AC-29, AC-30, AC-48, BASE-11 | uncommitted | `done` |
| [04](./tasks/task-04-unknown-customer-or-category.md) | Unknown customer or category is a field error | AC-31 | uncommitted | `done` |
| [05](./tasks/task-05-categories-endpoint.md) | **Unplanned** — expose the category list | supports AC-59 | uncommitted | `done` |
| [06](./tasks/task-06-ticket-detail-read.md) | **Unplanned** — read one ticket | AC-36; AC-35/AC-50 implemented, not claimed | uncommitted | `done` |

## Criteria delivered

| `AC-n` | Test naming it |
|---|---|
| AC-29 | `AC29_CreateTicket_ValidRequest_Returns201AsNewAndUnassigned`, `AC29_CreateTicket_IssuesUniqueReferences` |
| AC-30 | `AC30_CreateTicket_InvalidFields_Returns400KeyedByField`, `AC30_CreateTicket_SubjectOverLengthLimit_Returns400KeyedToSubject`, plus `AC30_Create_Rejects_*` at unit level |
| AC-31 | `AC31_CreateTicket_UnknownCustomer_Returns400KeyedToCustomerId`, `AC31_CreateTicket_UnknownCategory_Returns400KeyedToCategoryId`, `AC31_CreateTicket_BothUnknown_ReportsBothFields` |
| AC-36 | `AC36_GetTicket_UnknownId_Returns404` |
| AC-48 | `AC48_CreateTicket_PersistsOneCreatedHistoryRow` (integration) + four unit tests on the aggregate |
| AC-3 | `AC3_Tickets_WithoutAToken_Returns401` |
| A4 | `Categories_AreSeededAndListedForThePicker` |

## Deviations from the plan

**D1 — `NEXT VALUE FOR` cannot be run through `Database.SqlQuery<T>`. This cost the most time of
anything in Day 1.**

The generator was written as `db.Database.SqlQuery<long>($"SELECT NEXT VALUE FOR …")`, which is the
idiomatic EF way to read a scalar and which fails at runtime with **SQL Server error 11719**:

> NEXT VALUE FOR function is not allowed in check constraints, default objects, computed columns,
> views, user-defined functions, user-defined aggregates, user-defined table types, **sub-queries**,
> common table expressions, derived tables or return statements.

`SqlQuery<T>` composes the supplied text into a derived table (`SELECT … FROM (<sql>) AS x`) so it
can be further composed with LINQ — and that derived table is the sub-query the server refuses. The
statement has to reach SQL Server exactly as written, so the generator now issues a raw `DbCommand`
and reads it with `ExecuteScalarAsync`, enlisting in the ambient transaction when there is one.

**It surfaced as a plain 500 with no detail**, because the exception middleware converts everything
into the envelope. The diagnosis needed a temporary probe test that exercised the generator directly
outside the HTTP pipeline — the console-sink override CLAUDE.md documents did not reach the vitest
runner's captured output. Worth knowing before the next opaque 500.

**D2 — `GET /api/Categories` was missing from the plan entirely.**
Task 1.6 seeded the categories and nothing exposed them, which was not noticed until the frontend's
create form needed a picker. Without it the form would have had to offer free text, which `BR-14`
refuses. `GetCategoriesQuery` + `CategoriesController` were added — read-only, unpaged (a closed
list of four), `Authenticated`. **This is a plan defect, not a scope change:** `US-127` always
required a category picker and the backend plan simply failed to derive the endpoint from it.

**D3 — The category seeder had to be made race-tolerant.**
Read-then-insert, run by every host on start. xUnit runs test classes in parallel and each starts a
host, so the seeder immediately hit `UX_Categories_Name` and crashed start-up. It now catches
`DbUpdateException`, detaches the failed inserts, and **re-reads to confirm the rows really exist**
before continuing — rethrowing if they do not, so a genuine fault is not swallowed along with the
race. This is not a test-only concern: a rolling deploy starts two hosts at once and would have hit
exactly the same crash.

**D4 — Reference uniqueness is asserted; contiguity deliberately is not.**
`AC29_CreateTicket_IssuesUniqueReferences` checks format and distinctness only. `NEXT VALUE FOR`
does not join the caller's transaction, so a rejected create burns a number permanently. A test
asserting `TKT-001001` then `TKT-001002` would encode a guarantee the design explicitly refuses to
make, and would fail the first time any create was rejected.

Value 1000 was consumed verifying the migration, so the first real ticket is `TKT-001001`.

## Not done, and why

**No assignment or status-change endpoint.** `Ticket.AssignTo` and `Ticket.ChangeStatus` exist and
are unit-tested, but nothing exposes them. They belong to `FEAT-06` and `FEAT-07`; an endpoint here
would satisfy no criterion of this feature.

**Role vocabulary is unresolved.** The endpoints use the `Authenticated` policy. The platform seeds
`SuperAdmin`/`Admin`/`ContentManager`/`StateRepresentative`/`User`/`Visitor`; the spec's assumption
`A2` and criteria `AC-4`, `AC-42`, `AC-43`, `AC-47` all assume exactly `Agent` and `Supervisor`.
Day 1 needs neither — no criterion in `FEAT-03`/`04`/`05` restricts by role — so nothing was invented
here. **`FEAT-07` cannot start until this is decided**, and it is the first thing Day 2 hits.
