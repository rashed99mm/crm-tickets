# US-407 · Customer Reply to Agent

| Field | Value |
|---|---|
| **Story** | `US-407` |
| **Epic** | [EPIC-07 Customer Portal](../epics/EPIC-07.md) |
| **Feature** | [`FEAT-15` Customer Portal](../delivery-plan.md#feat-15--customer-portal) |
| **Layer** | Backend |
| **Ships with** | [US-409](./US-409-survey-endpoint.md) *(backend)* |
| **Actor** | Customer |
| **Priority** | P0 |
| **Sprint** | [10 — Customer portal](../delivery-plan.md#sprint-10-customer-portal) · Slice S3 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-8.5, BR-20 |
| **Spec criteria** | AC-7 |
| **Depends on** | [US-401](./US-401-customer-registration.md), [US-403](./US-403-customer-authorization.md), [US-404](./US-404-portal-submit-ticket.md), [US-406](./US-406-portal-ticket-detail.md) |

## Story

**As a customer**, **I want** to reply to my agent, **so that** I can provide information.

## Business rules

- BR-20 — Customer scoped to own records (BRD).
- BR-22 — Customer replies recorded as Messages with SenderType=Customer (BRD).

## Acceptance criteria

#### AC1 — Reply recorded against own ticket (spec AC-7)

Given customer authenticated, when reply submitted, then a Message is recorded against the customer's own ticket with SenderType=Customer.

## SQL tables

`Messages` — reply recorded:

```sql
CREATE TABLE [dbo].[Messages] (
    [Id]          UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [TicketId]    UNIQUEIDENTIFIER NOT NULL,
    [SenderId]    UNIQUEIDENTIFIER NOT NULL,
    [SenderType]  NVARCHAR(20)     NOT NULL DEFAULT 'Customer',
    [Content]     NVARCHAR(MAX)    NOT NULL,
    [CreatedAt]   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Messages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Messages_Tickets] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[Tickets] ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-7 | Integration | `ReplyToTicket_CreatesMessage` | Given customer owns ticket, when POST /api/portal/tickets/{id}/reply with content, then 201 | Message.TicketId == ticket.Id, SenderType == "Customer" |
| TC-02 | AC-7 | Integration | `ReplyToTicket_OtherCustomer_ReturnsForbidden` | Given customer A, when replying to customer B's ticket, then 403 | Authorization error returned |
| TC-03 | AC-7 | Integration | `ReplyToTicket_EmptyContent_Returns400` | Given empty content body, when reply submitted, then 400 Bad Request | Validation error for required field |
| TC-04 | AC-7 | Integration | `ReplyToTicket_NonexistentTicket_Returns404` | Given invalid ticket ID, when reply submitted, then 404 | Error message indicates ticket not found |

## Notes

Sender is derived from the JWT claim. No customer ID is accepted from the request body.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
