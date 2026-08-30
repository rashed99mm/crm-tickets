# US-508 · Helpfulness Vote

| Field | Value |
|---|---|
| **Story** | `US-508` |
| **Epic** | [EPIC-06 Knowledge Base](../epics/EPIC-06-knowledge-base.md) |
| **Feature** | [`FEAT-11` Knowledge base](../delivery-plan.md#feat-11--knowledge-base) |
| **Layer** | Backend |
| **Ships with** | [US-513](./US-513-portal-kb-browse.md) *(frontend)* |
| **Actor** | Customer |
| **Priority** | P1 |
| **Sprint** | [11 — Knowledge base](../delivery-plan.md#sprint-11-knowledge-base) · Slice S4 |
| **Estimate** | 2 points |
| **Status** | `not started` |
| **BRD requirements** | FR-6.7 |
| **Spec criteria** | AC-6.7 |
| **Depends on** | [US-501](./US-501-kb-publish-archive.md) |

## Story

**As a customer**, **I want** to rate articles as helpful or unhelpful, **so that** article quality improves over time.

## Business rules

- No BRD BR-n covers this directly. Each user may vote once per article.
- No BRD BR-n covers this directly. A user may change their vote, replacing the previous one.
- No BRD BR-n covers this directly. Helpful and unhelpful counts are stored on the article for fast reads.

## Acceptance criteria

#### AC1 — Vote helpful (spec AC-6.7)

Given a user viewing a Published article, when they vote "helpful", then the article's HelpfulCount is incremented by 1.

#### AC2 — Vote unhelpful

Given a user viewing a Published article, when they vote "unhelpful", then the article's UnhelpfulCount is incremented by 1.

#### AC3 — Change vote

Given a user who previously voted "helpful", when they change to "unhelpful", then HelpfulCount decrements by 1 and UnhelpfulCount increments by 1.

#### AC4 — One vote per user per article

Given a user who already voted on an article, when they vote again, then the previous vote is replaced.

## SQL tables

`ArticleVotes` — user votes on articles:

```sql
CREATE TABLE [dbo].[ArticleVotes] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [ArticleId]       UNIQUEIDENTIFIER NOT NULL,
    [UserId]          NVARCHAR(450)    NOT NULL,
    [IsHelpful]       BIT              NOT NULL,
    [VotedAt]         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_ArticleVotes] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_ArticleVotes_Article_User] UNIQUE ([ArticleId], [UserId]),
    CONSTRAINT [FK_ArticleVotes_KnowledgeArticles] FOREIGN KEY ([ArticleId]) REFERENCES [dbo].[KnowledgeArticles] ([Id])
);
```

`KnowledgeArticles` — denormalized vote count columns added to existing table:

```sql
ALTER TABLE [dbo].[KnowledgeArticles] ADD [HelpfulCount] INT NOT NULL DEFAULT 0;
ALTER TABLE [dbo].[KnowledgeArticles] ADD [UnhelpfulCount] INT NOT NULL DEFAULT 0;
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-6.7 | Unit | `VoteHelpful_IncrementsHelpfulCount` | Given article at HelpfulCount 3, when vote helpful, then HelpfulCount is 4 | HelpfulCount == 4 |
| TC-02 | AC-6.7 | Unit | `VoteUnhelpful_IncrementsUnhelpfulCount` | Given article at UnhelpfulCount 2, when vote unhelpful, then UnhelpfulCount is 3 | UnhelpfulCount == 3 |
| TC-03 | AC-6.7 | Unit | `ChangeVote_FromHelpfulToUnhelpful_AdjustsCounts` | Given user voted helpful, when changing to unhelpful, then HelpfulCount-- and UnhelpfulCount++ | Counts adjusted |
| TC-04 | AC-6.7 | Unit | `ChangeVote_FromUnhelpfulToHelpful_AdjustsCounts` | Given user voted unhelpful, when changing to helpful, then counts adjusted correctly | Counts adjusted |
| TC-05 | AC-6.7 | Unit | `Vote_CreatesVoteRecord` | Given user votes on article, when recorded, then vote record exists with UserId and IsHelpful | Record exists |
| TC-06 | AC-6.7 | Integration | `VoteEndpoint_Returns200_WhenHelpful` | Given article exists, when POST /api/kb/articles/{id}/vote with helpful, then 200 OK | 200 OK |
| TC-07 | AC-6.7 | Integration | `VoteEndpoint_Returns200_WhenChangingVote` | Given user previously voted, when changing vote, then 200 OK with updated counts | 200 OK |

## Notes

- The UNIQUE constraint on (ArticleId, UserId) enforces one vote per user per article.
- Vote changes are handled via upsert: insert or update on conflict.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
