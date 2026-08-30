# US-117 · A customer with history cannot be deleted

| Field | Value |
|---|---|
| **Story** | `US-117` *(was `US-1.20`)* |
| **Epic** | [EPIC-01 Customer management](../epics/EPIC-01-customer-management.md) |
| **Feature** | [`FEAT-03` Customer records](../delivery-plan.md#feat-03--customer-records) |
| **Layer** | Backend |
| **Ships with** | — API-only in S1. The spec defines no frontend criterion for customer management screens - see gap G-5. |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Team Lead |
| **Priority** | P0 |
| **Sprint** | [2 — Customers, ticket capture and queue](../delivery-plan.md#sprint-2--customers-ticket-capture-and-queue) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-1.8, FR-1.9, BR-7, BR-8 |
| **Spec criteria** | AC-15, AC-16 |
| **Depends on** | [US-009](./US-009-raise-a-ticket.md), [US-109](./US-109-auditing-and-soft-delete.md) *(sprint 1)* |

## Story

**As a supervisor**, **I want** deletion refused for any customer holding tickets, **so that** support history cannot be destroyed by one click.

## Business rules

- BR-7 — customer holding ≥1 ticket may not be deleted (BRD).
- BR-8 — deletion retains record; deleted customer's email reusable (BRD).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Guarded delete refuses with 409 (spec AC-15)

Given a customer with at least one ticket, when deleting, then 409 and the customer remains.

#### AC2 — Ticket-free delete soft-deletes (spec AC-16)

Given a customer with no tickets, when deleting, then 200 with a confirmation code, and it is gone
from listings — soft-deleted, so the row survives and its email becomes reusable.

## SQL tables

`Customers` delete path — from the [S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#customers),
plus the FK that backs the guard:

```sql
-- The guard is an application check, but the schema reinforces it:
[Tickets].[CustomerId] ... CONSTRAINT FK_Tickets_Customer
    REFERENCES [dbo].[Customers] ([Id])   -- no cascade anywhere

-- AC-16's soft delete flips these instead of removing the row:
UPDATE [dbo].[Customers] SET [IsDeleted]=1, [DeletedAtUtc]=…, [DeletedBy]=…
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-15 | Api.IntegrationTests | PASS `AC15_DeleteCustomer_WithTickets_Returns409AndCustomerRemains` (in `TicketEndpointTests` — needs a ticket) | a customer with ≥1 ticket / `DELETE` / inspect | 409, code `ERR012`; customer still listed |
| TC-02 | AC-16 | Api.IntegrationTests | PASS `AC16_DeleteCustomer_WithoutTickets_Returns200AndDisappearsFromList` | a ticket-free customer / `DELETE` / inspect | 200 with code `CON012`; gone from listings |
| TC-03 | AC-16 | Application.Tests | **superseded** — `InterceptorTests` was archived with the pre-baseline backend. The behaviour is now covered by `AC16_DeleteCustomer_WithoutTickets_…`, which re-reads after deleting | remove / save / query raw | row survives as soft-deleted |
| TC-04 | AC-16 | Api.IntegrationTests | **superseded** — `FilteredIndexTests` was archived. Now `AC16_CreateCustomer_EmailOfDeletedCustomer_Succeeds`, against real SQL Server | deleted email reused (real SQL Server) / insert / observe | accepted |
| TC-05 | AC-16 (end to end) | Api.IntegrationTests | PASS `AC16_CreateCustomer_EmailOfDeletedCustomer_Succeeds` | delete then re-create with the same email via the API / observe | 201 — closes the FND-5 no-204 rule too |

## Notes

**Ordering note.** This story's first criterion cannot be proven until a ticket can exist, so US-009 must land before this one even though its epic comes second in the sprint. The same inversion exists in the spec's own build order, which places the delete guard in step 2 and tickets in step 3; putting both epics in one sprint is what makes it resolvable rather than a cross-sprint dependency running backwards.

## Open questions

None.

## Status evidence

Implemented in `DeleteCustomerCommand` as an application guard, with `DeleteBehavior.Restrict` as
the database backstop. Nothing cascades.

AC-15 -> `AC15_DeleteCustomer_WithTickets_Returns409AndCustomerRemains`, which lives in
`TicketEndpointTests` because its precondition is a ticket. AC-16 ->
`AC16_DeleteCustomer_WithoutTickets_Returns200AndDisappearsFromList` and
`AC16_CreateCustomer_EmailOfDeletedCustomer_Succeeds` - the second is the criterion the filtered
unique index exists for.

Run 2026-08-26: 192 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
