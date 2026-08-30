# US-703 · Human Confirmation Gate

| Field | Value |
|---|---|
| **Story** | `US-703` |
| **Epic** | [EPIC-11 AI Features](../epics/EPIC-11-ai.md) |
| **Feature** | [`FEAT-20` AI-Powered Features](../delivery-plan.md#feat-20--ai-powered-features) |
| **Layer** | Backend |
| **Ships with** | [US-704](./US-704-ai-summarise.md) *(backend)*, [US-705](./US-705-ai-suggest-category.md) *(backend)*, [US-706](./US-706-ai-draft-reply.md) *(backend)*, [US-707](./US-707-ai-suggest-solution.md) *(backend)* |
| **Actor** | System |
| **Priority** | P2 |
| **Sprint** | [15 — AI assist](../delivery-plan.md#sprint-15-ai-assist) · Slice S7 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-7.6 |
| **Spec criteria** | AC-703 |
| **Depends on** | [US-702](./US-702-ai-service-port.md) |

## Story

**As a system**, **I want** AI suggestions to require human approval before being sent, **so that** nothing is auto-sent to a customer without review.

## Business rules

- BR-19 — No AI-generated response is sent to a customer without explicit human confirmation (BRD FR-7.6).
- No BRD BR-n covers this directly. AI suggestions are returned as drafts with a `status: pending_approval` state.

## Acceptance criteria

#### AC1 — Suggestions are drafts (spec AC-703)

Given an AI suggestion is generated, when it is returned to the agent, then it is in `pending_approval` status and is not sent to the customer.

#### AC2 — Agent confirms before send (spec AC-703)

Given a pending suggestion, when the agent confirms and sends it, then the message is sent to the customer and status changes to `sent`.

#### AC3 — Agent can reject (spec AC-703)

Given a pending suggestion, when the agent rejects it, then status changes to `rejected` and no message is sent.

## SQL tables

AI suggestions tracked in a new `AiSuggestions` table.

```sql
CREATE TABLE [dbo].[AiSuggestions] (
    [Id]                UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [TicketId]          UNIQUEIDENTIFIER NOT NULL,
    [Type]              NVARCHAR(50)     NOT NULL,
    [Suggestion]        NVARCHAR(MAX)    NOT NULL,
    [Status]            NVARCHAR(20)     NOT NULL DEFAULT ('pending_approval'),
    [CreatedByAgentId]  UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt]         DATETIME2        NOT NULL DEFAULT (GETUTCDATE()),
    [ConfirmedAt]       DATETIME2        NULL,
    CONSTRAINT [PK_AiSuggestions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AiSuggestions_Tickets] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[Tickets] ([Id]),
    CONSTRAINT [FK_AiSuggestions_Agents] FOREIGN KEY ([CreatedByAgentId]) REFERENCES [dbo].[Agents] ([UserId])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-703 | Unit | `SuggestionCreatedAsPending` | Given an AI suggestion is generated, when saved, then status is `pending_approval`. | Status = pending_approval |
| TC-02 | AC-703 | Integration | `SuggestionConfirmedSendsMessage` | Given a pending suggestion, when the agent confirms, then status becomes `sent` and a message is created on the ticket. | Message sent, status = sent |
| TC-03 | AC-703 | Integration | `SuggestionRejectedNoMessage` | Given a pending suggestion, when the agent rejects, then status becomes `rejected` and no message is created. | Status = rejected, no message |

## Notes

This is a critical safety gate. Every AI feature (reply, category, solution) must route through this confirmation flow. The gate is enforced at the application layer.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
