# US-412 · Portal My Tickets List

| Field | Value |
|---|---|
| **Story** | `US-412` |
| **Epic** | [EPIC-07 Customer Portal](../epics/EPIC-07.md) |
| **Feature** | [`FEAT-15` Customer Portal](../delivery-plan.md#feat-15--customer-portal) |
| **Layer** | Frontend |
| **Ships with** | [US-405](./US-405-portal-my-tickets.md) *(frontend)* |
| **Actor** | Customer |
| **Priority** | P0 |
| **Sprint** | [10 — Customer portal](../delivery-plan.md#sprint-10-customer-portal) · Slice S3 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-8.3 |
| **Spec criteria** | AC-12 |
| **Depends on** | [US-405](./US-405-portal-my-tickets.md) |

## Story

**As a customer**, **I want** to see my tickets with status, **so that** I know what is being handled.

## Business rules

None.

## Acceptance criteria

#### AC1 — Ticket list displays status and reference (spec AC-12)

Given customer authenticated, when viewing my-tickets, then list shows each ticket with status badge and reference number.

#### AC2 — Ticket list navigates to detail

Given ticket in list, when clicked, then navigates to ticket detail screen.

## SQL tables

None — frontend story.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-12 | Component | `TicketList_RendersTicketRows` | Given tickets returned from API, when component renders, then ticket rows visible | Each row has status and reference |
| TC-02 | AC-12 | Component | `TicketList_CallsApiOnLoad` | Given customer authenticated, when component loads, then GET /api/portal/tickets called | HTTP request fires |
| TC-03 | AC-2 | Component | `TicketList_ClickRow_NavigatesToDetail` | Given ticket row clicked, when click event fires, then router navigates to /portal/tickets/{id} | URL changes to detail route |
| TC-04 | AC-12 | Component | `TicketList_EmptyState_ShowsMessage` | Given no tickets, when component renders, then empty state message shown | "No tickets" text visible |

## Notes

Uses Angular signals for state management. Status badge uses color coding by status value.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
