# US-406 · Customer Ticket Detail

| Field | Value |
|---|---|
| **Story** | `US-406` |
| **Epic** | [EPIC-07 Customer Portal](../epics/EPIC-07.md) |
| **Feature** | [`FEAT-15` Customer Portal](../delivery-plan.md#feat-15--customer-portal) |
| **Layer** | Backend |
| **Ships with** | [US-407](./US-407-portal-reply.md) *(backend)* |
| **Actor** | Customer |
| **Priority** | P0 |
| **Sprint** | [10 — Customer portal](../delivery-plan.md#sprint-10-customer-portal) · Slice S3 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-8.3, FR-8.4, BR-20 |
| **Spec criteria** | AC-6 |
| **Depends on** | [US-401](./US-401-customer-registration.md), [US-403](./US-403-customer-authorization.md), [US-405](./US-405-portal-my-tickets.md) |

## Story

**As a customer**, **I want** to see my ticket's status and history, **so that** I understand progress.

## Business rules

- BR-20 — Customer scoped to own records (BRD).

## Acceptance criteria

#### AC1 — Ticket detail includes status, reference, and history (spec AC-6)

Given customer authenticated and owns the ticket, when viewing ticket detail, then status, reference number, and message history are returned.

## SQL tables

`Tickets` — ticket metadata:

```sql
CREATE TABLE [dbo].[Tickets] (
    [Id]              UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [Subject]         NVARCHAR(256)    NOT NULL,
    [Description]     NVARCHAR(MAX)    NULL,
    [Status]          NVARCHAR(50)     NOT NULL,
    [Priority]        NVARCHAR(50)     NOT NULL,
    [Channel]         NVARCHAR(50)     NOT NULL,
    [CustomerId]      UNIQUEIDENTIFIER NOT NULL,
    [ReferenceNumber] NVARCHAR(50)     NOT NULL,
    [CreatedAt]       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_Tickets_ReferenceNumber] UNIQUE ([ReferenceNumber]),
    CONSTRAINT [FK_Tickets_Customers] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers] ([Id])
);
```

`Messages` — conversation history:

```sql
CREATE TABLE [dbo].[Messages] (
    [Id]          UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [TicketId]    UNIQUEIDENTIFIER NOT NULL,
    [SenderId]    UNIQUEIDENTIFIER NOT NULL,
    [SenderType]  NVARCHAR(20)     NOT NULL,
    [Content]     NVARCHAR(MAX)    NOT NULL,
    [CreatedAt]   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Messages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Messages_Tickets] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[Tickets] ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-6 | Integration | `GetTicketDetail_ReturnsStatusAndReference` | Given customer owns ticket, when GET /api/portal/tickets/{id}, then 200 | Response has status, referenceNumber, subject |
| TC-02 | AC-6 | Integration | `GetTicketDetail_ReturnsMessageHistory` | Given ticket has messages, when detail fetched, then messages array included | Messages ordered by CreatedAt ascending |
| TC-03 | AC-6 | Integration | `GetTicketDetail_OtherCustomer_ReturnsForbidden` | Given customer A, when fetching customer B's ticket detail, then 403 | Authorization error returned |
| TC-04 | AC-6 | Integration | `GetTicketDetail_NonexistentTicket_Returns404` | Given invalid ticket ID, when GET /api/portal/tickets/{id}, then 404 | Error message indicates ticket not found |

## Notes

Message history should exclude system-generated messages unless they carry useful context for the customer.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
