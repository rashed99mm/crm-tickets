# US-201 · Record inbound/outbound message against ticket

| Field | Value |
|---|---|
| **Story** | `US-201` |
| **Epic** | [EPIC-03 Communication channels](../epics/EPIC-03-communication-channels.md) |
| **Feature** | [`FEAT-12` Customer notes](../delivery-plan.md#feat-12--customer-notes) |
| **Layer** | Backend |
| **Ships with** | [US-202](./EPIC-03-US-202-message-timeline.md) *(frontend)* |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [6 — Conversation record](../delivery-plan.md#sprint-6--conversation-record) · Slice S5 |
| **Estimate** | 3 points |
| **Status** | `done` — AC-101..AC-105, AC-109 (own numbering, spec [`EPIC-03-US-201-conversation-record.md`](../../superpowers/specs/EPIC-03-US-201-conversation-record.md)) |
| **BRD requirements** | FR-3.4 |
| **Spec criteria** | AC-3.4 |
| **Depends on** | [US-010](./US-010-ticket-detail.md) |

## Story

**As a support agent**, **I want** messages recorded against tickets, **so that** communication history is complete.

## Business rules

- BR-2 — Append-only history: every message appended to a ticket is permanent (BRD).

## Acceptance criteria

#### AC1 — Record message against ticket (spec AC-3.4)

Given a valid ticket and message metadata, when a message is recorded against the ticket, then the message is stored with `TicketId`, `Direction` (Inbound/Outbound), `Channel` (Email/System), `Subject`, `Body`, `SenderId`, and `SentAt`.

## SQL tables

`TicketMessages` — stores all communication records linked to a ticket:

```sql
CREATE TABLE [dbo].[TicketMessages] (
    [Id]                BIGINT            IDENTITY(1,1) NOT NULL,
    [TicketId]          BIGINT            NOT NULL,
    [Direction]         NVARCHAR(10)      NOT NULL,
    [Channel]           NVARCHAR(20)      NOT NULL,
    [Subject]           NVARCHAR(500)     NULL,
    [Body]              NVARCHAR(MAX)     NOT NULL,
    [SenderId]          UNIQUEIDENTIFIER  NOT NULL,
    [ExternalMessageId] NVARCHAR(256)     NULL,
    [SentAt]            DATETIME2         NOT NULL,
    [CreatedAt]         DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_TicketMessages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TicketMessages_Tickets] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[Tickets]([Id]),
    CONSTRAINT [FK_TicketMessages_Users] FOREIGN KEY ([SenderId]) REFERENCES [dbo].[Users]([Id])
);
CREATE INDEX [IX_TicketMessages_TicketId_SentAt] ON [dbo].[TicketMessages] ([TicketId], [SentAt]);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-3.4 | Unit | `RecordOutboundMessage_StoresCorrectFields` | Given a valid ticket and agent, when an outbound message is recorded, then all required fields are persisted | Message row contains correct TicketId, Direction='Outbound', Channel, Subject, Body, SenderId, SentAt |
| TC-02 | AC-3.4 | Unit | `RecordInboundMessage_StoresCorrectFields` | Given a valid ticket, when an inbound message is recorded, then Direction is 'Inbound' | Message row has Direction='Inbound' and correct SenderId |
| TC-03 | AC-3.4 | Integration | `RecordMessage_InvalidTicket_ThrowsNotFound` | Given a non-existent TicketId, when a message is recorded, then a NotFoundException is thrown | 404 returned, no message row created |

## Notes

- `SenderId` references the user who sent the message. For inbound customer messages this will be the customer user record.
- `ExternalMessageId` enables idempotent processing for inbound email ingestion (US-204).
- The message body is stored as-is; no sanitisation at this layer.

## Open questions

None.

## Status evidence

Implemented as `TicketMessage`/`RecordTicketMessageCommand`/`GetTicketMessagesQuery` — a superseding
design from `EPIC-03-US-201-conversation-record.md` rather than this file's original SQL sketch
(see that spec's A1/A4 for why: `BaseEntity`/`Guid` convention, sender always the acting agent).
`dotnet test CustomerSupport.slnx`: 295/295 passing, including `TicketMessagesEndpointTests` and
`TicketMessageTests`. Task record:
[`docs/superpowers/plans/EPIC-03-US-201-feat-14-conversation-record/README.md`](../../superpowers/plans/EPIC-03-US-201-feat-14-conversation-record/README.md).

Status is set from what is committed and executed, never from what is planned.
