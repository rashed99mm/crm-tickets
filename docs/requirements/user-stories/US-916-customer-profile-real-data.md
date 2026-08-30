# US-916 · Customer Profile: Real Data, No Dead Lanes

| Field | Value |
|---|---|
| **Story** | `US-916` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-30`](../delivery-plan.md#feat-30) |
| **Layer** | Frontend |
| **Ships with** | [US-912](./US-912-ticket-queue-redesign.md) *(Frontend)* |
| **Actor** | Agent |
| **Priority** | P1 |
| **Sprint** | 19 — UX redesign |
| **Estimate** | 4 points |
| **Status** | `not started` |

## Story

**As an agent**, **I want** a customer profile that shows what is actually known and places the
customer's tickets right there, **so that** I am not reading ten grey "not recorded" lanes or a lane
that can never load.

## Business rules

- Real fields always render (name, contact, id, created). The ten not-stored fields compress into a
  single "not recorded" group instead of ten placeholder lanes.
- The dead tickets lane is replaced: the queue's `customerId` filter (added for US-912) serves a real
  recent-tickets list; when none exist the empty state says so.

## Acceptance criteria

#### AC1 — Real profile layout

Given a customer profile, then stored fields render, not-stored fields form one compact group, and a
real recent-tickets list replaces the unavailable lane (or an honest empty state).

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Component | `CustomerDetail_GroupedNotRecorded` | one group, not lanes |
| TC-02 | AC1 | Component | `CustomerDetail_RecentTickets_FromRealEndpoint` | list from `/tickets?customerId=…` |
| TC-03 | AC1 | Component | `CustomerDetail_NoTickets_EmptyState` | honest empty message |

## SQL tables

None (frontend + `customerId` filter on the queue query added for US-912).

## Notes

Reworks the customer-profile screen around the real fields; uses `AsyncState` conventions throughout.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.