# US-507 · Record Article Views

| Field | Value |
|---|---|
| **Story** | `US-507` |
| **Epic** | [EPIC-06 Knowledge Base](../epics/EPIC-06-knowledge-base.md) |
| **Feature** | [`FEAT-11` Knowledge base](../delivery-plan.md#feat-11--knowledge-base) |
| **Layer** | Backend |
| **Ships with** | [US-513](./US-513-portal-kb-browse.md) *(frontend)* |
| **Actor** | System |
| **Priority** | P1 |
| **Sprint** | [11 — Knowledge base](../delivery-plan.md#sprint-11-knowledge-base) · Slice S4 |
| **Estimate** | 2 points |
| **Status** | `not started` |
| **BRD requirements** | FR-6.7 |
| **Spec criteria** | AC-6.7 |
| **Depends on** | [US-501](./US-501-kb-publish-archive.md) |

## Story

**As a system**, **I want** to record each time an article is viewed, **so that** usage analytics are available for content improvement.

## Business rules

- No BRD BR-n covers this directly. Each page load of a published article is recorded as a view.
- No BRD BR-n covers this directly. View count is stored on the article and in a separate analytics table for detail.

## Acceptance criteria

#### AC1 — View increments counter (spec AC-6.7)

Given a Published article, when a user views it, then the article's ViewCount is incremented by 1.

#### AC2 — View record is stored

Given an article is viewed, when the view is recorded, then a record is created with the article ID, user ID (if authenticated), and timestamp.

#### AC3 — Anonymous views are tracked

Given an article is viewed by an anonymous user, when the view is recorded, then the user ID is null but the view is still counted.

## SQL tables

`ArticleViews` — individual view records:

```sql
CREATE TABLE [dbo].[ArticleViews] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [ArticleId]       UNIQUEIDENTIFIER NOT NULL,
    [UserId]          NVARCHAR(450)    NULL,
    [ViewedAt]        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_ArticleViews] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ArticleViews_KnowledgeArticles] FOREIGN KEY ([ArticleId]) REFERENCES [dbo].[KnowledgeArticles] ([Id])
);
```

`KnowledgeArticles` — ViewCount column added to existing table:

```sql
ALTER TABLE [dbo].[KnowledgeArticles] ADD [ViewCount] INT NOT NULL DEFAULT 0;
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-6.7 | Unit | `RecordView_IncrementsArticleViewCount` | Given article at ViewCount 5, when viewed, then ViewCount is 6 | ViewCount == 6 |
| TC-02 | AC-6.7 | Unit | `RecordView_CreatesViewRecord` | Given article viewed by user X, when recorded, then view record exists with UserId X | Record exists |
| TC-03 | AC-6.7 | Unit | `RecordView_AnonymousUser_UserIdIsNull` | Given article viewed anonymously, when recorded, then UserId is null | UserId is null |
| TC-04 | AC-6.7 | Unit | `RecordView_SetsTimestamp` | Given article viewed, when recorded, then ViewedAt is set | ViewedAt is not null |
| TC-05 | AC-6.7 | Integration | `ViewEndpoint_IncrementsCount` | Given article exists, when GET /api/kb/articles/{id}/view, then ViewCount incremented | ViewCount incremented |
| TC-06 | AC-6.7 | Integration | `ViewEndpoint_Returns200` | Given article exists, when viewed, then 200 OK | 200 OK |

## Notes

- ViewCount on the article is a denormalized counter for fast reads; ArticleViews provides the detail.
- Rate limiting should be considered to prevent view count manipulation.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
