# US-221 · Supervisor Override of Auto-Assignment

| Field | Value |
|---|---|
| **Story** | `US-221` |
| **Epic** | [EPIC-05 SLA & Escalation](../epics/EPIC-05-sla-and-automation.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Backend |
| **Ships with** | [US-220](./EPIC-05-US-220-auto-assignment.md) *(backend)* |
| **Actor** | Supervisor |
| **Priority** | P1 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 2 points |
| **Status** | `done` |
| **BRD requirements** | FR-5.10 |
| **Spec criteria** | AC-5.10 |
| **Depends on** | [US-220](./EPIC-05-US-220-auto-assignment.md) |

## Story

**As a supervisor**, **I want** to override auto-assignment and manually reassign tickets, **so that** exceptions and special cases are handled appropriately.

## Business rules

- BR-10 — Supervisor assigns and reassigns tickets (BRD).
- BR-26 — Only supervisors and admins can override auto-assignment (BRD).
- BR-27 — Override reassignment is logged in the ticket audit trail (BRD).

## Acceptance criteria

#### AC1 — Manual Reassign Ticket (spec AC-5.10)

Given a supervisor is authenticated, when they reassign a ticket to a specific agent, then the ticket is reassigned to that agent and the action is logged in the audit trail.

#### AC2 — Disable Auto-Assignment for Ticket (spec AC-5.10)

Given a supervisor is authenticated, when they disable auto-assignment for a specific ticket, then the ticket remains with the current agent and is excluded from future auto-assignment cycles.

## SQL tables

No new tables — uses existing `Tickets.AssignedTo` and audit trail infrastructure.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5.10 | Integration | `ReassignTicket_ShouldUpdateAssignment` | Given a supervisor reassigns a ticket to Agent B, when the request is processed, then the ticket's `AssignedTo` is updated to Agent B | `Tickets.AssignedTo` = Agent B |
| TC-02 | AC-5.10 | Integration | `ReassignTicket_ShouldLogAuditEntry` | Given a ticket is reassigned by a supervisor, when the audit trail is checked, then an override entry is present with the supervisor's identity | Audit entry present |
| TC-03 | AC-5.10 | Integration | `NonSupervisor_ShouldNotReassign` | Given a non-supervisor user, when they attempt to reassign a ticket, then access is denied | 403 Forbidden |

## Notes

Override reassignment bypasses the auto-assignment logic. The ticket's auto-assignment exclusion flag ensures the system does not re-assign it in the next cycle.

## Open questions

None.

## Status evidence

**Already shipped by `FEAT-07`, found on re-examination during `FEAT-17`'s second slice rather
than rebuilt.** All three of this story's own test cases already proven:
`AssignTicketCommandHandler` sets `AssigneeId` (TC-01), every assign/reassign is appended to
`TicketHistory` (TC-02), and `AC43_Agent_AssigningAnyTicket_Returns403` proves Supervisor-only
(TC-03). See `docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-escalation/README.md`.

Status is set from what is committed and executed, never from what is planned.
