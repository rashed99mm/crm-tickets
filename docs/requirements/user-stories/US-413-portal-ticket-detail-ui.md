# US-413 · Portal Ticket Detail Screen

| Field | Value |
|---|---|
| **Story** | `US-413` |
| **Epic** | [EPIC-07 Customer Portal](../epics/EPIC-07.md) |
| **Feature** | [`FEAT-15` Customer Portal](../delivery-plan.md#feat-15--customer-portal) |
| **Layer** | Frontend |
| **Ships with** | [US-406](./US-406-portal-ticket-detail.md) *(frontend)* |
| **Actor** | Customer |
| **Priority** | P0 |
| **Sprint** | [10 — Customer portal](../delivery-plan.md#sprint-10-customer-portal) · Slice S3 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-8.3, FR-8.4 |
| **Spec criteria** | AC-13 |
| **Depends on** | [US-406](./US-406-portal-ticket-detail.md) |

## Story

**As a customer**, **I want** to see ticket detail with status, reference and history, **so that** I understand progress.

## Business rules

None.

## Acceptance criteria

#### AC1 — Detail screen shows status, reference, and message history (spec AC-13)

Given customer viewing own ticket detail, when screen loads, then status, reference number, and conversation history are displayed.

#### AC2 — Detail screen shows reply form for open tickets

Given ticket status is not Closed, when detail loads, then reply form is available.

## SQL tables

None — frontend story.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-13 | Component | `TicketDetail_ShowsStatusAndReference` | Given ticket data loaded, when rendered, then status and reference visible | Both values displayed in UI |
| TC-02 | AC-13 | Component | `TicketDetail_ShowsMessageHistory` | Given messages returned, when rendered, then conversation list visible | Messages ordered chronologically |
| TC-03 | AC-13 | Component | `TicketDetail_CallsApiOnLoad` | Given valid ticket ID in route, when component loads, then GET /api/portal/tickets/{id} called | HTTP request fires |
| TC-04 | AC-2 | Component | `TicketDetail_OpenTicket_ShowsReplyForm` | Given ticket status "Open", when rendered, then reply form visible | Reply input and submit button exist |
| TC-05 | AC-2 | Component | `TicketDetail_ClosedTicket_HidesReplyForm` | Given ticket status "Closed", when rendered, then reply form not shown | Reply input absent from DOM |

## Notes

Screen combines ticket metadata, message timeline, and conditional reply form. Uses route param for ticket ID.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
