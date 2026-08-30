# Task 1 — The status-change command and endpoint

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — tasks 1.1, 2.1–2.3 |
| Feature | `FEAT-06` Ticket detail and lifecycle |
| Criteria | `AC-35`, `AC-37`, `AC-36` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommand.cs`
- `src/CustomerSupport.Application/Features/Tickets/Validators/TicketCommandValidators.cs`
- `src/CustomerSupport.Application/Features/Tickets/Dtos/TicketDtos.cs` (`RowVersion`, `ChangeTicketStatusRequest`)
- `src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` (`ChangeStatus`)
- `tests/CustomerSupport.Tests/Integration/TicketLifecycleEndpointTests.cs`

## Test evidence

- `AC35_GetTicket_ReturnsCustomerSummaryAndHistoryNewestFirst` — the criterion `FEAT-04` implemented
  but deliberately did not claim, now claimed with a test naming it
- `AC37_ChangeStatus_PermittedTransition_Returns200AndPersists` — 6 parameterised cases, each
  re-fetching to prove persistence rather than trusting the 200
- `AC36_ChangeStatus_UnknownTicket_Returns404`

Suite: **233 passed, 0 failed.**

## Deviations from the plan

**1. `POST /api/Tickets/{id}/status`, not `PATCH /api/Tickets/{id}`.**
Planned as a sub-resource and delivered as one. A status change is a **transition**, not a field
assignment: `PATCH { "status": "Closed" }` reads as setting a value, and the whole design refuses
that reading. The endpoint's shape is part of the rule.

**2. The detail DTO gained `RowVersion`, base64.**
Not a separate task in the plan; folded in here because `AC-41` cannot work without the client
having a version to echo, and the detail read is where it gets one. Opaque by design — nothing but
the server interprets it.

## The point of this task

**`AC-35` was already implemented and is only now delivered.** `FEAT-04` built `GetTicketByIdQuery`
because a 201 carrying an id cannot demonstrate that a ticket starts `New` and unassigned, and that
query returned the customer summary and history all along. Its record said so and explicitly refused
to claim `AC-35` or `AC-50`.

That refusal is what this task settles. A shape that happens to satisfy a criterion is not a
criterion that has been proven, and the difference is a test that names it.
