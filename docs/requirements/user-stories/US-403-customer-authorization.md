# US-403 · Customer-Scoped Authorization

| Field | Value |
|---|---|
| **Story** | `US-403` |
| **Epic** | [EPIC-07 Customer Portal](../epics/EPIC-07.md) |
| **Feature** | [`FEAT-15` Customer Portal](../delivery-plan.md#feat-15--customer-portal) |
| **Layer** | Backend |
| **Ships with** | — |
| **Actor** | System |
| **Priority** | P0 |
| **Sprint** | [10 — Customer portal](../delivery-plan.md#sprint-10-customer-portal) · Slice S3 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-8.9, BR-20 |
| **Spec criteria** | AC-3 |
| **Depends on** | [US-401](./US-401-customer-registration.md) |

## Story

**As a system**, **I want** customers scoped to their own records, **so that** data isolation is enforced.

## Business rules

- BR-20 — Customer scoped to own records (BRD).

## Acceptance criteria

#### AC1 — Cross-customer access returns 403 (spec AC-3)

Given a customer token, when accessing another customer's data, then 403 Forbidden is returned.

## SQL tables

None — authorization enforced at query level by filtering on `CustomerId` from JWT claim.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-3 | Integration | `AccessOwnTickets_ReturnsOk` | Given customer A token, when GET /api/portal/tickets, then 200 with customer A's tickets only | All returned tickets belong to customer A |
| TC-02 | AC-3 | Integration | `AccessOtherCustomerTickets_ReturnsForbidden` | Given customer A token, when GET /api/portal/tickets/{customerB-ticketId}, then 403 | Response body contains authorization error |
| TC-03 | AC-3 | Integration | `AccessOtherCustomerDetail_ReturnsForbidden` | Given customer A token, when GET /api/portal/tickets/{customerB-ticketId}/detail, then 403 | Response body contains authorization error |

## Notes

Authorization middleware extracts customerId from JWT claim. All portal endpoints apply a `Where(t => t.CustomerId == customerId)` filter or equivalent.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
