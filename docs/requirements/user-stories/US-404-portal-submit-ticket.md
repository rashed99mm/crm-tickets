# US-404 · Customer Submits Ticket Through Portal

| Field | Value |
|---|---|
| **Story** | `US-404` |
| **Epic** | [EPIC-07 Customer Portal](../epics/EPIC-07.md) |
| **Feature** | [`FEAT-15` Customer Portal](../delivery-plan.md#feat-15--customer-portal) |
| **Layer** | Backend |
| **Ships with** | [US-405](./US-405-portal-my-tickets.md) *(backend)*, [US-406](./US-406-portal-ticket-detail.md) *(backend)* |
| **Actor** | Customer |
| **Priority** | P0 |
| **Sprint** | [10 — Customer portal](../delivery-plan.md#sprint-10-customer-portal) · Slice S3 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-8.2, BR-20 |
| **Spec criteria** | AC-4 |
| **Depends on** | [US-401](./US-401-customer-registration.md), [US-403](./US-403-customer-authorization.md) |

## Story

**As a customer**, **I want** to submit a request through the portal, **so that** my issue is tracked.

## Business rules

- BR-20 — Customer scoped to own records (BRD).
- BR-21 — Portal-submitted tickets stamped with channel=Portal (BRD).

## Acceptance criteria

#### AC1 — Ticket created with channel=Portal (spec AC-4)

Given customer authenticated, when ticket submitted via portal, then ticket is created with channel set to Portal and CustomerId set from JWT.

## SQL tables

`Tickets` — support ticket:

```sql
CREATE TABLE [dbo].[Tickets] (
    [Id]              UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [Subject]         NVARCHAR(256)    NOT NULL,
    [Description]     NVARCHAR(MAX)    NULL,
    [Status]          NVARCHAR(50)     NOT NULL DEFAULT 'New',
    [Priority]        NVARCHAR(50)     NOT NULL DEFAULT 'Medium',
    [Channel]         NVARCHAR(50)     NOT NULL DEFAULT 'Portal',
    [CustomerId]      UNIQUEIDENTIFIER NOT NULL,
    [AssignedAgentId] UNIQUEIDENTIFIER NULL,
    [ReferenceNumber] NVARCHAR(50)     NOT NULL,
    [CreatedAt]       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [UpdatedAt]       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_Tickets_ReferenceNumber] UNIQUE ([ReferenceNumber]),
    CONSTRAINT [FK_Tickets_Customers] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers] ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-4 | Integration | `SubmitTicket_SetsChannelPortal` | Given authenticated customer, when POST /api/portal/tickets with valid payload, then 201 | Ticket.Channel == "Portal" |
| TC-02 | AC-4 | Integration | `SubmitTicket_SetsCustomerIdFromJwt` | Given authenticated customer, when ticket created, then CustomerId matches JWT claim | Ticket.CustomerId == jwt.customerId |
| TC-03 | AC-4 | Integration | `SubmitTicket_GeneratesReferenceNumber` | Given ticket created, when created, then ReferenceNumber is non-null and unique | ReferenceNumber format matches pattern |
| TC-04 | AC-4 | Integration | `SubmitTicket_Unauthenticated_Returns401` | Given no token, when POST /api/portal/tickets, then 401 Unauthorized | Response indicates authentication required |

## Notes

Reference number format should follow existing convention from the internal API ticket creation flow.

## Open questions

None.

## Status evidence

Implemented through the portal submit screen and the shared ticket creation flow. The server derives
the authenticated customer's `CustomerId`, stamps channel `Portal`, starts the ticket at `New`, and
returns the created ticket id in the standard response envelope.

Status is set from what is committed and executed, never from what is planned.
