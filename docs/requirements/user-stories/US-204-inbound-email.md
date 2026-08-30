# US-204 · Inbound email ingestion

| Field | Value |
|---|---|
| **Story** | `US-204` |
| **Epic** | [EPIC-03 Communication channels](../epics/EPIC-03-communication-channels.md) |
| **Feature** | *(no frontend feature — background process)* |
| **Layer** | Backend |
| **Ships with** | No frontend counterpart (background process) |
| **Actor** | System |
| **Priority** | P0 |
| **Sprint** | [9 — Email channel](../delivery-plan.md#sprint-9--email-channel) · Slice S5 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-3.2 |
| **Spec criteria** | AC-3.2 |
| **Depends on** | [US-201](./US-201-record-message.md), [US-203](./EPIC-03-US-203-email-provider.md) |

## Story

**As a system**, **I want** inbound email creating or updating tickets, **so that** customer replies are captured.

## Business rules

- BR-15 — Unique reference per ticket: the email subject ticket reference uniquely identifies the target ticket (BRD).
- BR-20 — Customer scoped to own records: inbound messages from an unknown sender are rejected or create a new customer (BRD).

## Acceptance criteria

#### AC1 — Create ticket from inbound email (spec AC-3.2)

Given an inbound email with no matching ticket reference, when processed, then a new ticket is created and the email body is recorded as the first message.

#### AC2 — Append reply to existing ticket (spec AC-3.2)

Given an inbound email referencing an existing ticket, when processed, then the email body is recorded as an inbound message on that ticket.

#### AC3 — Idempotent processing (spec AC-3.2)

Given an inbound email that has already been processed (same `ExternalMessageId`), when processed again, then no duplicate ticket or message is created.

#### AC4 — Failure handling

Given an inbound email fails to process, when the error is transient, then the email is retried; when non-transient, then it is logged and a dead-letter record is created.

## SQL tables

`TicketMessages` and `Tickets` — read from US-201. `EmailIngestionLog` tracks processing attempts for idempotency and dead-lettering:

```sql
CREATE TABLE [dbo].[EmailIngestionLog] (
    [Id]                BIGINT           IDENTITY(1,1) NOT NULL,
    [ExternalMessageId] NVARCHAR(256)    NOT NULL,
    [FromAddress]       NVARCHAR(256)    NOT NULL,
    [Subject]           NVARCHAR(500)    NOT NULL,
    [TicketId]          BIGINT           NULL,
    [Status]            NVARCHAR(20)     NOT NULL,
    [ErrorMessage]      NVARCHAR(MAX)    NULL,
    [ProcessedAt]       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_EmailIngestionLog] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmailIngestionLog_Tickets] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[Tickets]([Id])
);
CREATE UNIQUE INDEX [IX_EmailIngestionLog_ExternalMessageId] ON [dbo].[EmailIngestionLog] ([ExternalMessageId]);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-3.2 | Unit | `InboundEmail_NoMatch_CreatesNewTicket` | Given an inbound email with no ticket reference, when processed, then a new ticket and first message are created | Ticket row created; TicketMessages row with Direction='Inbound' |
| TC-02 | AC-3.2 | Unit | `InboundEmail_ExistingTicket_AppendsMessage` | Given an inbound email referencing ticket T-001, when processed, then message is added to T-001 | No new ticket; new TicketMessages row linked to T-001 |
| TC-03 | AC-3.2 | Unit | `InboundEmail_DuplicateMessageId_NoDuplicatesCreated` | Given an email with ExternalMessageId already in EmailIngestionLog, when processed again, then no new records created | Status remains 'Processed'; no duplicate rows |
| TC-04 | AC-3.2 | Integration | `InboundEmail_ProcessingPipeline_E2E` | Given a mock inbound email feed, when emails arrive, then tickets and messages are created correctly | Correct number of tickets and messages; idempotency holds |
| TC-05 | AC-3.2 | Unit | `InboundEmail_TransientFailure_Retries` | Given email processing throws a transient exception, when retried, then processing succeeds on second attempt | EmailIngestionLog shows final status 'Processed' |

## Notes

- Ticket reference extraction relies on a convention in the email subject (e.g., `[TICKET-{Id}]`) or an `X-Ticket-Id` header. The exact format should match the outbound email template from US-205.
- The ingestion endpoint is an anonymous webhook or background poller — no user authentication is required at the ingestion boundary.
- Dead-letter records must be queryable for admin review.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
