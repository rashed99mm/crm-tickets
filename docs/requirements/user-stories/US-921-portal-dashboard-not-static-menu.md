# US-921 · Portal Dashboard: Not a Static Menu

| Field | Value |
|---|---|
| **Story** | `US-921` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-30`](../delivery-plan.md#feat-30) |
| **Layer** | Frontend |
| **Ships with** | (none) |
| **Actor** | Customer |
| **Priority** | P1 |
| **Sprint** | 19 — UX redesign |
| **Estimate** | 4 points |
| **Status** | `not started` |

## Story

**As a customer**, **I want** my portal dashboard to show my actual tickets and next step, **so that**
it is not three static cards.

## Business rules

- Portal dashboard shows the signed-in customer's open tickets (via `/api/tickets?customerId=…` on
  the portal surface or its portal equivalent), a quick-submit link and a KB entry — all real data.
- Keeps `AsyncState` conventions and i18n/RTL; `home` landing stays a marketing page.

## Acceptance criteria

#### AC1 — Real portal dashboard

Given a signed-in customer, then the portal dashboard lists their real open tickets, a quick-submit
link and KB search, from live data.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Component | `PortalDashboard_ShowsCustomerTickets` | tickets from endpoint fixture |
| TC-02 | AC1 | Component | `PortalDashboard_Empty_NeverLooksLikeError` | empty state distinct from error |

## SQL tables

None.

## Notes

Reworks the portal dashboard from static cards to a data view; reuses the portal ticket list API.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.