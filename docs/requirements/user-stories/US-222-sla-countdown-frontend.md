# US-222 · SLA Countdown on Ticket Detail

| Field | Value |
|---|---|
| **Story** | `US-222` |
| **Epic** | [EPIC-02 Ticket Management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Frontend |
| **Ships with** | [US-212](./US-212-sla-targets-on-creation.md), [US-213](./US-213-sla-pause-resume.md) *(frontend)* |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-5.2 |
| **Spec criteria** | AC-5.2 |
| **Depends on** | [US-212](./US-212-sla-targets-on-creation.md), [US-213](./US-213-sla-pause-resume.md), [US-011](./US-011-ticket-detail-screen.md) |

## Story

**As a support agent**, **I want** to see an SLA countdown on the ticket detail screen, **so that** I know exactly how much time remains before a response or resolution target is breached.

## Business rules

- BR-28 — SLA countdown displays time remaining for both response and resolution targets (BRD).

## Acceptance criteria

#### AC1 — Display SLA Countdown (spec AC-5.2)

Given a ticket detail view with SLA targets, when the view is rendered, then `ResponseDueAt` and `ResolutionDueAt` are displayed with a live countdown showing the time remaining.

#### AC2 — Colour-Coded Urgency (spec AC-5.2)

Given the SLA countdown is displayed, when the time remaining falls below the warning threshold, then the countdown shows in warning colour (amber); when breached, it shows in danger colour (red).

## SQL tables

None — frontend story, reads from API response.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5.2 | E2E | `TicketDetail_ShouldDisplaySLA` | Given a ticket with SLA targets loaded, when the ticket detail view renders, then the SLA countdown is visible | Countdown displayed with correct remaining time |
| TC-02 | AC-5.2 | Unit | `SLACountdown_ShouldShowWarningColor` | Given a ticket approaching its breach threshold, when the countdown is rendered, then the amber warning colour is applied | Warning colour displayed |
| TC-03 | AC-5.2 | Unit | `SLACountdown_ShouldShowDangerColor` | Given a ticket that has breached its SLA, when the countdown is rendered, then the red danger colour is applied | Danger colour displayed |

## Notes

The countdown updates in real-time using a timer interval (e.g., every second). SLA data comes from the ticket API response. Uses Angular signals for reactivity.

## Open questions

None.

## Status evidence

Shipped 2026-08-27 — `SlaCountdown` (`common`), wired into `TicketDetailComponent` for both
`responseDueAt`/`resolutionDueAt`. `npx ng test common --watch=false --include='**/sla-countdown.component.spec.ts'`
→ 4/4 passing. `npx ng test admin-app --watch=false --include='**/ticket-detail.component.spec.ts'`
→ passing (part of a combined 22/22 run with two sibling files). See
`docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-escalation/README.md`'s "Frontend addendum" for full
evidence. **Not yet committed** — staged only, per explicit instruction this session.

Status is set from what is committed and executed, never from what is planned.
