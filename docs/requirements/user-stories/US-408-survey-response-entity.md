# US-408 · SurveyResponse Entity

| Field | Value |
|---|---|
| **Story** | `US-408` |
| **Epic** | [EPIC-07 Customer Portal](../epics/EPIC-07.md) |
| **Feature** | [`FEAT-15` Customer Portal](../delivery-plan.md#feat-15--customer-portal) |
| **Layer** | Backend |
| **Ships with** | [US-409](./US-409-survey-endpoint.md) *(backend)* |
| **Actor** | System |
| **Priority** | P1 |
| **Sprint** | [10 — Customer portal](../delivery-plan.md#sprint-10-customer-portal) · Slice S3 |
| **Estimate** | 2 points |
| **Status** | `not started` |
| **BRD requirements** | FR-8.7 |
| **Spec criteria** | AC-8 |
| **Depends on** | [US-404](./US-404-portal-submit-ticket.md) |

## Story

**As a system**, **I want** to store satisfaction ratings, **so that** CSAT is measurable.

## Business rules

- BR-23 — Rating must be an integer between 1 and 5 inclusive (BRD).
- BR-24 — One survey response per resolved ticket (BRD).

## Acceptance criteria

#### AC1 — SurveyResponse entity stores rating and metadata (spec AC-8)

Given a SurveyResponse, when stored, then it contains TicketId, Rating (1-5), FreeText, and CreatedAt.

## SQL tables

`SurveyResponses` — customer satisfaction survey:

```sql
CREATE TABLE [dbo].[SurveyResponses] (
    [Id]          UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [TicketId]    UNIQUEIDENTIFIER NOT NULL,
    [Rating]      INT              NOT NULL,
    [FreeText]    NVARCHAR(2000)   NULL,
    [CreatedAt]   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_SurveyResponses] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_SurveyResponses_TicketId] UNIQUE ([TicketId]),
    CONSTRAINT [FK_SurveyResponses_Tickets] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[Tickets] ([Id]),
    CONSTRAINT [CK_SurveyResponses_Rating] CHECK ([Rating] BETWEEN 1 AND 5)
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-8 | Unit | `SurveyResponse_ValidRating_Accepted` | Given rating 3, when entity created, then Rating property is 3 | No validation error |
| TC-02 | AC-8 | Unit | `SurveyResponse_RatingBelow1_Throws` | Given rating 0, when entity created, then validation exception | Property remains unset or default |
| TC-03 | AC-8 | Unit | `SurveyResponse_RatingAbove5_Throws` | Given rating 6, when entity created, then validation exception | Property remains unset or default |
| TC-04 | AC-8 | Unit | `SurveyResponse_FreeTextOptional` | Given null FreeText, when entity created, then entity is valid | FreeText is null |
| TC-05 | AC-8 | Integration | `SurveyResponse_DuplicateTicketId_Throws` | Given existing response for ticket, when inserting second response for same ticket, then unique constraint violation | DbUpdateException thrown |

## Notes

EF Core entity should enforce Rating range via domain validation. The UNIQUE constraint on TicketId enforces one survey per ticket.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
