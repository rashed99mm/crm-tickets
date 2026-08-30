# Task 2 — Refuse undefined transitions, and keep 409 distinct from 400

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — tasks 3.1–3.5 |
| Feature | `FEAT-06` Ticket detail and lifecycle |
| Criteria | `AC-38`, `AC-39`, `AC-30` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommand.cs`
- `src/CustomerSupport.Application/Features/Tickets/Validators/TicketCommandValidators.cs`
- `src/CustomerSupport.Api.Shared/Localization/Resources.yaml`

## Test evidence

- `AC38_ChangeStatus_UndefinedTransition_Returns409NotValidationError` — the three pairs the spec
  names by hand: `New → Closed`, `Closed → Resolved`, `New → Resolved`
- `AC38_RefusedTransition_ChangesNothing` — status **and** history count unchanged on re-fetch
- `AC39_ChangeStatus_ToTheStatusAlreadyHeld_Returns409` — 3 cases
- `AC30_ChangeStatus_UnknownStatusValue_Returns400NotConflict`

Suite: **233 passed, 0 failed.**

## The distinction this task exists to protect

Two refusals arrive at the same endpoint and must not answer alike:

| Request | Answer | Why |
|---|---|---|
| `Closed` from `New` | **409** | The status exists; the *state* is wrong |
| `Escalated` from anything | **400** | There is no such status; the *request* is wrong |

Collapsing them would be easy and would look tidy. It would also destroy the contrast `AC-38` is
built on — the criterion says "not 400: the request is well-formed, the state is wrong", which only
means something if a malformed request is 400.

So "is this a real status" lives in `ChangeTicketStatusCommandValidator`, and "may this ticket go
there" lives in the handler. `AC30_…Returns400NotConflict` is named for the thing it is guarding
against, not just the thing it asserts.

## Deviations from the plan

**1. The handler classifies before delegating, purely to pick an error code.**
`Ticket.ChangeStatus` throws one `InvalidOperationException` for both refusals, so the handler
consults `TicketStatus` first: equal statuses → `TICKET_ALREADY_IN_STATUS` (`AC-39`), otherwise not
in the table → `TICKET_TRANSITION_NOT_ALLOWED` (`AC-38`).

**The aggregate is still the enforcement.** This classification is a presentation concern and does
not decide anything — if it were deleted the transition would still be refused, just with a vaguer
code. That ordering matters: a handler that decided the rule itself would be the bypass the private
setter exists to prevent.

**2. `AC-66`'s numbered codes are not what the platform emits.**
`AC-66` fixes `ERR021`/`ERR022`/`ERR024`. The adopted platform uses named codes throughout, so these
are `TICKET_TRANSITION_NOT_ALLOWED`, `TICKET_ALREADY_IN_STATUS` and
`TICKET_MODIFIED_BY_ANOTHER_USER`, each with an `ar`/`en` pair. **This is a real divergence from an
approved criterion**, it dates from the baseline adoption rather than from this task, and it belongs
to `FEAT-09`'s hardening pass. Recorded rather than silently renamed.
