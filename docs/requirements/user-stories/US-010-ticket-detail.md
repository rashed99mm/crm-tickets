# US-010 · Open a ticket and see its whole story

| Field | Value |
|---|---|
| **Story** | `US-010` *(was `US-1.24`)* — rule proposal: *View Ticket* |
| **Epic** | [EPIC-02 Ticket management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-06` Ticket detail and lifecycle](../delivery-plan.md#feat-06--ticket-detail-and-lifecycle) |
| **Layer** | Backend |
| **Ships with** | [US-128](./US-128-ticket-detail-with-guarded-actions.md) *(frontend)* |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [3 — Ticket detail, lifecycle, assignment and history](../delivery-plan.md#sprint-3--ticket-detail-lifecycle-assignment-and-history) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-2.7, FR-4.2 |
| **Spec criteria** | AC-35, AC-36 |
| **Depends on** | [US-009](./US-009-raise-a-ticket.md) |

## Story

**As an agent**, **I want** the ticket, who it is for, and what has happened to it on one screen, **so that** I do not have to ask the customer to repeat themselves.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- A fetched ticket carries its customer summary and its history newest first (from AC-35).
- An unknown ticket id answers 404 (from AC-36).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Detail carries summary and history (spec AC-35)

Given a ticket id, when fetching, then the ticket with a customer summary and its history, newest
first.

#### AC2 — Unknown ticket is 404 (spec AC-36)

Given an unknown ticket id, then 404.

## SQL tables

`Tickets` + `TicketHistory` read path — from the
[S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#tickethistory):

```sql
CREATE INDEX IX_TicketHistory_Ticket_Occurred
    ON [dbo].[TicketHistory] ([TicketId], [OccurredAtUtc] DESC);
-- detail joins: Tickets → Customers (summary), TicketHistory (newest first)
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-35 | Api.IntegrationTests | PASS `AC35_GetTicket_ReturnsCustomerSummaryAndHistoryNewestFirst` | a ticket with history rows / `GET /tickets/{id}` / inspect | ticket + customer summary + history, newest first |
| TC-02 | AC-35 | Application.Tests | PASS covered by TC-01's ordering assertion (`BeInDescendingOrder(OccurredAt)`) — the query orders, not the caller | history entries out of order / read via the handler / inspect | ordered newest first by the query, not the caller |
| TC-03 | AC-36 | Api.IntegrationTests | PASS `AC36_GetTicket_UnknownId_Returns404`. **Code is `TICKET_NOT_FOUND`, not `ERR020`** — see the AC-66 divergence | unknown ticket id / fetch / observe | 404, code `ERR020` |

## Notes

The customer summary travels with the ticket rather than requiring a second call, because the agent needs both together every single time and two round trips is two chances to render a half-populated screen.

## Open questions

None.

## Status evidence

Implemented in `GetTicketByIdQuery` (built in FEAT-04, claimed here).

AC-35 -> `AC35_GetTicket_ReturnsCustomerSummaryAndHistoryNewestFirst`; AC-36 ->
`AC36_GetTicket_UnknownId_Returns404` and `AC36_ChangeStatus_UnknownTicket_Returns404`.

The query existed from FEAT-04, which needed a read endpoint to verify what it created and
deliberately declined to claim AC-35 there. This story is where a test names it.

Run 2026-08-26: `dotnet test CustomerSupport.slnx` - 233 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
