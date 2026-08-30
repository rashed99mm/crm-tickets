# Task 2 — Close the finding: a deactivated agent could still be handed work

| Field | Value |
|---|---|
| Plan | [`implementation-plan.md`](../implementation-plan.md) |
| Story | `MVP-02` Administer staff accounts and roles |
| Criterion | 4 — "given a deactivated agent, they are no longer offered as an assignee" |
| Status | `done` — defect found and fixed |
| Commit | uncommitted — working tree |

## The defect

`GetUsersInRoleAsync` filters on `IsActive`, so `GET /api/Tickets/assignable-agents` correctly stops
offering a deactivated agent. **`AssignTicketCommandHandler` did not check `IsActive` at all.** It
verified that the target existed and held the `Agent` role, and then assigned the ticket.

So the picker hid them and the mutation accepted them. A supervisor holding a page rendered before
the deactivation, a stale client, or anything calling the API directly could hand work to an account
that can no longer sign in to do it — a ticket that would sit in someone's queue forever, assigned
and unworkable. The two halves of the criterion disagreed, and only the cosmetic half was true.

The plan expected criterion 4 to hold, and it had a reason to: `GetAssignableAgentsQuery`'s own doc
comment asserts that the picker and the mutation enforce the same filter. They did for the role
check. They did not for `IsActive`, and nothing tested it — which is exactly how this survived
`FEAT-07`.

## Evidence — the failing test, before the fix

`MVP02_DeactivatedAgent_CannotBeHandedWorkDirectly`, run against the code as inherited:

```
[xUnit.net 00:00:04.28]     StaffAdministrationTests.MVP02_DeactivatedAgent_CannotBeHandedWorkDirectly [FAIL]
  Failed StaffAdministrationTests.MVP02_DeactivatedAgent_CannotBeHandedWorkDirectly [563 ms]
  Error Message:
   Expected assign.StatusCode to be HttpStatusCode.BadRequest {value: 400} because work cannot be
   handed to an account that can no longer sign in to do it, but found HttpStatusCode.OK {value: 200}.

Failed!  - Failed: 1, Passed: 0, Skipped: 0, Total: 1
```

**200 OK.** The assignment succeeded against a deactivated agent.

The test was written before the fix and was not touched afterwards. That ordering is the whole
method: had the check been added first, the test would have been written around the fixed behaviour
and the fact that this ever shipped broken would be invisible.

## The fix

| File | Change |
|---|---|
| `backend/src/CustomerSupport.Application/Features/Tickets/Commands/AssignTicket/AssignTicketCommand.cs` | after the `Agent` role check, refuse a target whose `IsActive` is false — a field-keyed 400 on `AssigneeId`, matching every other refusal of that field |
| `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs` | new code `Ticket.ASSIGNEE_DEACTIVATED = "TICKET_ASSIGNEE_DEACTIVATED"` |
| `backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml` | its Arabic and English messages |
| `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetAssignableAgents/GetAssignableAgentsQuery.cs` | doc comment corrected: the list narrows the choice, it does not enforce it |

**A distinct error code rather than reusing `ASSIGNEE_NOT_AN_AGENT`.** A deactivated agent *is* an
agent, and "that person has left" is a different thing for a supervisor to read than "that person
was never in this role" — the second reads as a bug in the picker. `US-107` requires every code to
carry a bilingual message, and `EveryErrorCode_HasABilingualMessage` enforces it, so the catalogue
entry is part of the fix rather than a follow-up.

**400 keyed to `AssigneeId`, not 403 or 409.** The assignee is named in the request *body* and the
addressed resource — the ticket — exists, which is the same reasoning `AC-31` and `AC-44` already
apply on this endpoint. Keying it to the field lets the assign form land the message on the picker
that caused it.

## Verification

```
Passed  StaffAdministrationTests.MVP02_DeactivatedAgent_CannotBeHandedWorkDirectly  [500 ms]
Passed  StaffAdministrationTests.MVP02_DeactivatedAgent_IsNotOfferedAsAnAssignee    [218 ms]
```

Whole suite:

```
Passed!  - Failed:     0, Passed:   270, Skipped:     0, Total:   270, Duration: 1 m 26 s
```

The `FEAT-07` assignment tests still pass unchanged: they assign to active agents, which is the path
this fix does not touch.

## Not changed, deliberately

Tickets **already assigned** to someone who is then deactivated stay assigned. Reassigning them
automatically would be a workload decision nobody has specified, and silently clearing the assignee
would destroy the very history criterion 2 exists to protect. A supervisor reassigns them, and the
queue's `unassigned` filter is not the place that surfaces them.
