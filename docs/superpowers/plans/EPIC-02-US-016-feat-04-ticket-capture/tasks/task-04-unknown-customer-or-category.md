# Task 4 — An unknown customer or category is a field error, not a 404

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — task 1.5 |
| Feature | `FEAT-04` Ticket capture |
| Criteria | `AC-31` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommand.cs`
- `tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

## Test evidence

- `AC31_CreateTicket_UnknownCustomer_Returns400KeyedToCustomerId`
- `AC31_CreateTicket_UnknownCategory_Returns400KeyedToCategoryId`
- `AC31_CreateTicket_BothUnknown_ReportsBothFields`

Suite: **193 passed, 0 failed.**

## Deviations from the plan

**1. Both references are checked before either is reported.**
The obvious implementation returns on the first missing id. The plan's task 1.5 said "both unknown →
both entries", so the handler accumulates into a dictionary and reports once. A form that has to be
submitted twice to learn about two problems is the failure `AC-8` and `AC-30` exist to prevent, and
there is no reason `AC-31` should behave differently.

**2. The category check also requires `IsActive`.**
Not stated in the plan. A deactivated category is not a valid choice for a *new* ticket even though
the row exists, and admitting one would let the closed vocabulary be reopened through a stale id.

**3. Field keys are PascalCase — `CustomerId`, not `customerId`.**
The dictionary is built by hand here rather than by FluentValidation, so the casing had to be chosen
deliberately to match what the validation pipeline produces elsewhere. Getting this wrong would have
produced a field key no control binds to, which fails silently: the request is still a 400 and the
message still travels, it just never reaches the input that caused it. The Angular envelope
interceptor lowercases the first character on the way in.

## The point of this task

This is where the spec's 400-versus-404 rule is implemented, and the rule is what makes `AC-31` and
`AC-12` consistent rather than contradictory:

> A resource named in the **path** that does not exist is 404. A resource referenced in a **request
> body** that does not exist is a field-keyed 400 — because the addressed resource (the ticket
> collection) does exist, and the payload is what is wrong.

The failure is emitted as `ErrorType.Validation`, so it flows through the same mapping as every other
field error and lands on the form control named `customerId`. The test names say `Returns400` rather
than `ReturnsNotFound` so the intent survives a later refactor by someone who has not read this file.
