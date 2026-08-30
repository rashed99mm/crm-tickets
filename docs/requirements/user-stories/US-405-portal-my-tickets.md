# US-405 · Customer's Own Tickets List

| Field | Value |
|---|---|
| **Story** | `US-405` |
| **Epic** | [EPIC-07 Customer Portal](../epics/EPIC-07.md) |
| **Feature** | [`FEAT-15` Customer Portal](../delivery-plan.md#feat-15--customer-portal) |
| **Layer** | Backend |
| **Ships with** | [US-406](./US-406-portal-ticket-detail.md) *(backend)* |
| **Actor** | Customer |
| **Priority** | P0 |
| **Sprint** | [10 — Customer portal](../delivery-plan.md#sprint-10-customer-portal) · Slice S3 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-8.3, FR-8.4, BR-20 |
| **Spec criteria** | AC-5 |
| **Depends on** | [US-401](./US-401-customer-registration.md), [US-403](./US-403-customer-authorization.md), [US-404](./US-404-portal-submit-ticket.md) |

## Story

**As a customer**, **I want** to see my tickets, **so that** I know what is being handled.

## Business rules

- BR-20 — Customer scoped to own records (BRD).

## Acceptance criteria

#### AC1 — Only own tickets returned with status and reference (spec AC-5)

Given customer authenticated, when querying own tickets, then only that customer's tickets are returned with status and reference number.

## SQL tables

`Tickets` — queried with WHERE CustomerId = @CustomerId:

```sql
CREATE TABLE [dbo].[Tickets] (
    [Id]              UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [Subject]         NVARCHAR(256)    NOT NULL,
    [Status]          NVARCHAR(50)     NOT NULL,
    [Channel]         NVARCHAR(50)     NOT NULL,
    [CustomerId]      UNIQUEIDENTIFIER NOT NULL,
    [ReferenceNumber] NVARCHAR(50)     NOT NULL,
    [CreatedAt]       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_Tickets_ReferenceNumber] UNIQUE ([ReferenceNumber]),
    CONSTRAINT [FK_Tickets_Customers] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers] ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5 | Integration | `GetMyTickets_ReturnsOnlyOwnTickets` | Given customer A with 3 tickets, when GET /api/portal/tickets, then 200 with exactly 3 tickets | All tickets.CustomerId == customerA.Id |
| TC-02 | AC-5 | Integration | `GetMyTickets_ExcludesOtherCustomerTickets` | Given customer A and B each have tickets, when A queries, then B's tickets not present | Response contains zero tickets from customer B |
| TC-03 | AC-5 | Integration | `GetMyTickets_ReturnsStatusAndReference` | Given tickets exist, when queried, then each item has status and referenceNumber fields | Fields are non-null |
| TC-04 | AC-5 | Integration | `GetMyTickets_EmptyResult_ReturnsEmptyArray` | Given customer with no tickets, when GET /api/portal/tickets, then 200 with empty array | Response is `[]` |

## Notes

Response DTO should include ticketId, subject, status, referenceNumber, createdAt. Pagination may be added later.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
