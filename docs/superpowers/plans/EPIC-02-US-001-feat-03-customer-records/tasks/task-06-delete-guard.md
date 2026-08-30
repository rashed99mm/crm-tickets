# Task 5 — The delete guard

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — US-117, tasks 5.1–5.3 |
| Feature | `FEAT-03` Customer records |
| Criteria | `AC-15`, `AC-16`, `AC-12` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Customers/Commands/DeleteCustomer/DeleteCustomerCommand.cs`
- `src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketConfiguration.cs` (`DeleteBehavior.Restrict`)
- `tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs` — `AC-15`
- `tests/CustomerSupport.Tests/Integration/CustomerEndpointTests.cs` — `AC-16`, `AC-12`

## Test evidence

- `AC15_DeleteCustomer_WithTickets_Returns409AndCustomerRemains` — 409 `CUSTOMER_HAS_TICKETS`, then re-fetches to prove the customer is still there
- `AC16_DeleteCustomer_WithoutTickets_Returns200AndDisappearsFromList` — 200 (not 204), gone from listings, 404 on re-fetch
- `AC16_CreateCustomer_EmailOfDeletedCustomer_Succeeds`
- `AC12_DeleteCustomer_UnknownId_Returns404`

Suite: **193 passed, 0 failed.**

## Deviations from the plan

**1. `AC-15`'s test lives in `TicketEndpointTests`, not with the rest of the customer tests.**
Its precondition is a ticket, which cannot exist until `FEAT-04` does. The plan predicted this — it
is the stated reason the two features share a sprint — and the test simply lives with the fixture
that can satisfy it. Named `AC15_…` so a traceability search still finds it from the criterion.

**2. Two of `US-117`'s test-case rows were already marked done, against tests that no longer exist.**
TC-03 cited `InterceptorTests.Remove_Becomes_A_Soft_Delete_And_The_Row_Survives` and TC-04 cited
`FilteredIndexTests.…`, both archived with the pre-baseline backend. They were reading as passing
evidence for code that is gone. Both rows are now marked **superseded** and point at the tests that
actually cover the behaviour today. This is the failure mode `CLAUDE.md` warns about — a story
reading `done` against evidence that evaporated — and it was found by reading the rows rather than
trusting them.

## The point of this task

The guard is an **application check, not a database cascade**, and the distinction is the whole
story. `IX_Tickets_CustomerId` exists so the check is cheap; `DeleteBehavior.Restrict` is the
backstop that turns a missed check into an error rather than into silently destroyed support
history. Neither is a substitute for the other: the FK cannot produce a 409 with a useful message,
and the handler cannot protect a path that bypasses it.

`AC-16` returning **200 rather than 204** is deliberate (FND-5): every response carries a code and a
message, and 204 has no body to carry them in.
