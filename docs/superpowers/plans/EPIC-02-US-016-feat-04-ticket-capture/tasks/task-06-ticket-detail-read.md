# Task 6 — Read one ticket · **not in the plan, and it reaches into FEAT-06**

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — **no corresponding task** |
| Feature | `FEAT-04` Ticket capture, anticipating `FEAT-06` / `FEAT-08` |
| Criteria | `AC-36` fully; `AC-35` and `AC-50` **partially, ahead of their feature** |
| Status | `done` for `AC-36`; the rest is unclaimed until FEAT-06 proves it |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQuery.cs`
- `src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` (`GetById`)
- `tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

## Test evidence

- `AC36_GetTicket_UnknownId_Returns404`
- Used as the verification step by `AC29_CreateTicket_ValidRequest_Returns201AsNewAndUnassigned`,
  `AC29_CreateTicket_IssuesUniqueReferences` and `AC48_CreateTicket_PersistsOneCreatedHistoryRow`

Suite: **193 passed, 0 failed.**

## Why this task exists, and why it is uncomfortable

`AC-29` requires a created ticket to be `New`, unassigned, and to carry a generated reference.
**None of that is observable from a 201 response carrying only an id.** Either the test reaches into
the database directly — which tests the persistence layer rather than the API contract — or a read
endpoint exists.

`CreatedAtAction` also needs a named action to point its `Location` header at, so `AC-7`'s ticket
equivalent needs `GetById` to exist regardless.

**The uncomfortable part:** the DTO returns the customer summary and the history timeline, which is
`AC-35` and `AC-50` — criteria belonging to `FEAT-06` and `FEAT-08`. That is scope leaking forward.
It is recorded here rather than quietly absorbed, and the honest position is:

- `AC-36` is **claimed and tested** by this feature.
- `AC-35` and `AC-50` are **implemented but not claimed**. Their stories stay `not started` until
  `FEAT-06` writes tests naming them — a shape that happens to satisfy a criterion is not the same as
  a criterion that has been proven.

The narrower alternative — returning only the ticket's own columns — was rejected because `FEAT-06`
would rewrite the endpoint days later, and the wider shape costs nothing extra to serve.

## Deviations from the plan

**1. Actor display names come from `IIdentityUserService`, one lookup per distinct actor.**
`ApplicationUser` is `IdentityUser<Guid>` and therefore outside `IRepository<T>`'s `BaseEntity`
constraint, so there is no queryable to join against from the Application layer. A ticket's history
is a handful of rows with two or three actors, so this is a few reads rather than an N+1 — but it
**would become one** if history ever grew unbounded, and that is the point at which it needs
revisiting. Noted rather than pre-optimised.

**2. Missing customer or category degrade to empty strings rather than failing.**
A ticket whose customer row vanished is a data-integrity problem, not a reason to 500 on a read.
`DeleteBehavior.Restrict` should make it impossible; the null-coalescing is belt and braces.
