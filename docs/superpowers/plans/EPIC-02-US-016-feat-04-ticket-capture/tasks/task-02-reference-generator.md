# Task 2 — Issue human-readable ticket references

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — tasks 1.3, part of 1.2 |
| Feature | `FEAT-04` Ticket capture |
| Criteria | `AC-29`, `BR-15` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Domain/Interfaces/ITicketReferenceGenerator.cs`
- `src/CustomerSupport.Infrastructure/Persistence/TicketReferenceGenerator.cs`
- `src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs` (`HasSequence`)
- `src/CustomerSupport.Infrastructure/Migrations/20260825170424_AddTicketWorkflow.cs`

## Test evidence

`AC29_CreateTicket_IssuesUniqueReferences` — two creates, distinct references, both matching
`^TKT-\d{6}$`. Suite: **193 passed, 0 failed.**

## Deviations from the plan

**1. `Database.SqlQuery<T>` cannot run `NEXT VALUE FOR`. This cost more time than anything else in
Day 1.**

The generator was written the idiomatic way:

```csharp
db.Database.SqlQuery<long>($"SELECT NEXT VALUE FOR [dbo].[TicketReferenceSequence] AS [Value]")
```

SQL Server rejects it with **error 11719**: `NEXT VALUE FOR` is not allowed in "sub-queries, common
table expressions, derived tables…". `SqlQuery<T>` composes the supplied text into a derived table
(`SELECT … FROM (<sql>) AS x`) so the result stays composable with LINQ — and that derived table is
the sub-query the server refuses. The statement has to reach SQL Server exactly as written.

Rewritten as a raw `DbCommand` read with `ExecuteScalarAsync`, opening the connection only if it was
closed and enlisting in the ambient transaction when there is one.

**2. Diagnosing it needed a throwaway probe test.**
It surfaced as a bare **500** with no detail, because the exception middleware converts everything
into the envelope — the same failure signature `CLAUDE.md` already warns about for a missing
`Jwt:Key`. The documented console-sink override
(`Serilog__Using__0=Serilog.Sinks.Console`) did **not** reach the test runner's captured output, so
the fix was a temporary test that called `ITicketReferenceGenerator` directly, outside the HTTP
pipeline, and printed the exception. Deleted afterwards. Worth knowing before the next opaque 500.

## The point of this task

**The test asserts uniqueness and format, and deliberately not contiguity.** `NEXT VALUE FOR` is
atomic and does not join the caller's transaction, so a rolled-back create burns a number
permanently. That is the correct trade — gaps in a reference series are unremarkable, two customers
quoting `TKT-001042` is not — and a test asserting `001001` then `001002` would encode a guarantee
the design explicitly refuses to make, failing the first time any create was rejected.

A sequence rather than `MAX(Reference) + 1` because the latter races under concurrent inserts, and
`UX_Tickets_Reference` would turn that race into a 500.

Value 1000 was consumed verifying the migration, so the first real ticket is `TKT-001001`.

The port lives in `Domain/Interfaces` and the implementation in `Infrastructure`: the Domain must not
know SQL Server exists.
