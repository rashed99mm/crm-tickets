# US-219 · Notify on Breach/Imminent Breach

| Field | Value |
|---|---|
| **Story** | `US-219` |
| **Epic** | [EPIC-05 SLA & Escalation](../epics/EPIC-05-sla-and-automation.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Backend |
| **Ships with** | [US-216](./US-216-sla-breach-detection.md), [US-217](./EPIC-05-US-217-sla-warning.md) *(backend)* |
| **Actor** | Supervisor |
| **Priority** | P0 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-5.8 |
| **Spec criteria** | AC-5.8 |
| **Depends on** | [US-201](./US-201-notification-service.md) *(Notifications)* |

## Story

**As a supervisor**, **I want** to receive breach and imminent-breach notifications, **so that** I can take action before SLA targets are missed or immediately after they are breached.

## Business rules

- BR-22 — Breach notifications are sent to the assignee, their supervisor, and any escalation target (BRD).
- BR-23 — Notification channel (email, in-app) is configurable per organisation (BRD).

## Acceptance criteria

#### AC1 — Breach Notification (spec AC-5.8)

Given a ticket SLA breach is detected, when the breach notification is triggered, then the assignee and supervisor receive a notification containing the ticket ID, subject, and breach information.

#### AC2 — Imminent Breach Notification (spec AC-5.8)

Given a ticket approaching its SLA breach threshold, when the pre-breach warning threshold is crossed, then the assignee and supervisor receive a warning notification indicating the time remaining before breach.

## SQL tables

None — reuses existing Notifications infrastructure from the CCE Platform.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5.8 | Unit | `BreachNotification_ShouldIncludeTicketDetails` | Given an SLA breach detected on a ticket, when the notification is constructed, then the notification includes ticket ID, subject, and breach time | Notification content contains required fields |
| TC-02 | AC-5.8 | Integration | `BreachNotification_ShouldDeliverToAssigneeAndSupervisor` | Given a breached ticket with an assigned agent and supervisor, when the notification is dispatched, then both the assignee and supervisor receive the notification | Two notifications delivered |
| TC-03 | AC-5.8 | Unit | `ImminentBreachNotification_ShouldUseWarningTemplate` | Given a pre-breach warning threshold crossed, when the notification is sent, then the warning template is used instead of the breach template | Warning notification sent with correct template |

## Notes

Reuses the existing notification service from the CCE Platform. Notification templates should be added for SLA breach and imminent-breach warning scenarios. The warning threshold is configurable per SLA policy.

## Open questions

None.

## Status evidence

Implemented through `NotificationGateway`: breach notifications include the ticket reference and
escalation detail, while imminent warnings include the target type and minutes remaining. Delivery
correlation is stable per ticket/target, and each status transition that pauses SLA is respected by
the scanner.

Status is set from what is committed and executed, never from what is planned.
