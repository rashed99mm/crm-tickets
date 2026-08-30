# US-216 · SLA Breach Detection Background Job

| Field | Value |
|---|---|
| **Story** | `US-216` |
| **Epic** | [EPIC-05 SLA & Escalation](../epics/EPIC-05.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Backend |
| **Ships with** | [US-219](./US-219-sla-breach-notifications.md) *(Backend)* |
| **Actor** | System |
| **Priority** | P0 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 8 points |
| **Status** | `done` |
| **BRD requirements** | FR-5.5, FR-5.6, BR-18 |
| **Spec criteria** | AC-5.5, AC-5.6 |
| **Depends on** | [US-211](./US-211-sla-event-entity.md), [US-212](./US-212-sla-targets-on-creation.md), [US-215](./EPIC-05-US-215-business-hours-calendar.md) |

## Story

**As a system**, **I want** to continuously monitor SLA clocks, **so that** breaches are detected promptly.

## Business rules

- BR-18 — SLA breach detection runs as a background job at a configurable interval (BRD).

## Acceptance criteria

#### AC1 — Detect SLA Breaches (spec AC-5.5)

Given a ticket with an SLA target, when the target time passes, then a breach event is recorded in SLAEvents and a notification is sent.

#### AC2 — Detect Imminent Breaches (spec AC-5.6)

Given a ticket approaching breach threshold (configurable warning percentage), when the threshold is crossed, then a pre-breach warning notification is sent.

## SQL tables

Uses `Tickets`, `SLAEvents`, and `SLAPolicies` tables defined in prior stories.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5.5 | Unit | `Monitor_ShouldDetectBreaches` | Given ticket past SLA due date, when job runs, then SLAEvent is recorded with BreachedAt set | Breach event persisted |
| TC-02 | AC-5.5 | Integration | `Monitor_ShouldSendBreachNotification` | Given ticket breaches SLA, when job runs, then notification is sent | Notification delivered |
| TC-03 | AC-5.6 | Unit | `Monitor_ShouldDetectImminentBreaches` | Given ticket within warning threshold, when job runs, then pre-breach warning is sent | Warning notification delivered |
| TC-04 | AC-5.5 | Unit | `AC133_WaitingForCustomerTicket_IsNotEvaluated` | Given ticket in Waiting for Customer with paused SLA, when the scanner runs, then the ticket is not treated as breached | Ticket skipped |

## Notes

The job is implemented as the `SlaBreachDetector` hosted service and scans every five minutes by
default (`SlaAutomation:ScanIntervalMinutes`). Tickets with `PausedAt` set, including both waiting
statuses, are not evaluated.

## Open questions

None.

## Status evidence

Shipped `FEAT-17` first slice — `SlaBreachScanner`/`SlaBreachDetector`, a `BackgroundService`
polling loop recording an `SLAEvent` when a `New`/`Open` ticket's due date has passed. Registered
on both hosts (matching `NotificationSender`'s actual precedent, not the spec's original
internal-host-only assumption — a deviation recorded in the same README). See
`docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-tracking/README.md`.

Status is set from what is committed and executed, never from what is planned.
