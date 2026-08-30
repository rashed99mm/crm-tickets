# US-217 · SLA Pre-Breach Warning

| Field | Value |
|---|---|
| **Story** | `US-217` |
| **Epic** | [EPIC-05 SLA & Escalation](../epics/EPIC-05.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Backend |
| **Ships with** | [US-219](./US-219-sla-breach-notifications.md) *(Backend)* |
| **Actor** | Supervisor |
| **Priority** | P1 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-5.9, BR-20 |
| **Spec criteria** | AC-5.9 |
| **Depends on** | [US-216](./US-216-sla-breach-detection.md) |

## Story

**As a supervisor**, **I want** pre-breach warnings, **so that** I can intervene before SLA is missed.

## Business rules

- BR-20 — Warning threshold is configurable as a percentage of total SLA time remaining (BRD).

## Acceptance criteria

#### AC1 — Pre-Breach Warning Notification (spec AC-5.9)

Given a ticket approaching breach threshold, when the warning is triggered, then a notification is sent to the ticket's assignee and their supervisor.

## SQL tables

No new tables. Uses existing `Tickets`, `SLAEvents`, and notification infrastructure.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5.9 | Unit | `WarningNotification_ShouldNotifyAssigneeAndSupervisor` | Given ticket approaching breach, when warning is triggered, then both assignee and supervisor are notified | Two notifications sent |
| TC-02 | AC-5.9 | Unit | `WarningNotification_ShouldNotRepeatWithinCooldown` | Given warning already sent, when job runs again within cooldown period, then no duplicate warning is sent | No duplicate notification |
| TC-03 | AC-5.9 | Unit | `WarningNotification_UnassignedTicket_ShouldNotifySupervisorOnly` | Given unassigned ticket approaching breach, when warning is triggered, then only supervisor is notified | One notification to supervisor |

## Notes

Warning cooldown prevents notification spam. The supervisor is determined by the ticket's assigned agent's supervisor relationship (or branch supervisor fallback).

## Open questions

None.

## Status evidence

Implemented in `SlaBreachScanner`: the warning window is configurable through
`SlaAutomation:WarningPercentage` (default `0.8`), targets the assignee and Supervisor role, and
uses `NotificationDelivery.CorrelationId` for durable repeat suppression. Waiting tickets are not
considered warning or breach candidates.

Status is set from what is committed and executed, never from what is planned.
