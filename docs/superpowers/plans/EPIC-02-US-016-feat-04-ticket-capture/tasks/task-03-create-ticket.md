# Task 3 — Raise a ticket

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — tasks 1.2, 1.4, 1.6, 1.7 |
| Feature | `FEAT-04` Ticket capture |
| Criteria | `AC-29`, `AC-30`, `AC-48`, `BASE-11` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommand.cs`
- `src/CustomerSupport.Application/Features/Tickets/Validators/TicketValidators.cs`
- `src/CustomerSupport.Application/Features/Tickets/Dtos/TicketDtos.cs`
- `src/CustomerSupport.InternalApi/Controllers/TicketsController.cs`
- `tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

## Test evidence

- `AC29_CreateTicket_ValidRequest_Returns201AsNewAndUnassigned` — 201, `Location`, status `New`, null assignee, `TKT-` reference
- `AC30_CreateTicket_InvalidFields_Returns400KeyedByField` — `Subject`, `Description` and `Priority` in one response
- `AC30_CreateTicket_SubjectOverLengthLimit_Returns400KeyedToSubject`
- `AC48_CreateTicket_PersistsOneCreatedHistoryRow` — exactly one row, `Created`, `ToValue = New`

Plus the Phase 0 unit tests on the aggregate. Suite: **193 passed, 0 failed.**

## Deviations from the plan

**1. The priority validator reads the value object rather than listing the four values.**
`TicketPriority.TryCreate` is the single source. A validator with its own
`Must(p => p is "Low" or "Normal" or …)` would be a second list to keep in step, and the two would
eventually disagree — the queue's filter validator has the same need and shares the same source.

## The point of this task

**`AC-48` is satisfied by the aggregate, not by this handler.** `Ticket.Create` appends its own
`Created` history row, so there is no code path that produces a ticket without one — the handler
could not forget to write it even if it tried. The integration test exists to prove the row is
actually *persisted* through the `HasMany`/backing-field mapping, which is the part unit tests cannot
see.

The actor comes from `IUserContext.UserId` and never from the payload (`BR-6`). Nothing in
`CreateTicketRequest` names an actor, so a caller attempting to attribute a ticket to someone else
has nowhere to put the value.

**Assignment and status changes are deliberately absent.** `Ticket.AssignTo` and
`Ticket.ChangeStatus` exist and are unit-tested from Phase 0, but nothing exposes them: they belong
to `FEAT-06` and `FEAT-07`, and an endpoint here would satisfy no criterion of this feature.
