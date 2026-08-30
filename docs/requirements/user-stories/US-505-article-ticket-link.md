# US-505 · Article-to-Ticket Linking

| Field | Value |
|---|---|
| **Story** | `US-505` |
| **Epic** | [EPIC-06 Knowledge Base](../epics/EPIC-06-knowledge-base.md) |
| **Feature** | [`FEAT-11` Knowledge base](../delivery-plan.md#feat-11--knowledge-base) |
| **Layer** | Backend |
| **Ships with** | [US-509](./US-509-kb-admin-list.md) *(frontend)* |
| **Actor** | Agent |
| **Priority** | P1 |
| **Sprint** | [11 — Knowledge base](../delivery-plan.md#sprint-11-knowledge-base) · Slice S4 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-6.5 |
| **Spec criteria** | AC-6.5 |
| **Depends on** | [US-501](./US-501-kb-publish-archive.md) |

## Story

**As an agent**, **I want** to link a KB article as the solution to a ticket, **so that** deflection rate is measurable.

## Business rules

- No BRD BR-n covers this directly. An agent may link one or more articles to a ticket as the solution.
- No BRD BR-n covers this directly. Each link records which agent linked it and when.
- No BRD BR-n covers this directly. A ticket may have zero or more solution articles.

## Acceptance criteria

#### AC1 — Link article to ticket (spec AC-6.5)

Given a ticket and a Published article, when an agent links the article as solution, then a link record is created.

#### AC2 — Link records agent and timestamp

Given an article is linked to a ticket, when the link is created, then it stores the linking agent's ID and the timestamp.

#### AC3 — Unlink article from ticket

Given a ticket-article link exists, when an agent unlinks it, then the link record is removed.

#### AC4 — List linked articles for ticket

Given a ticket with linked articles, when queried, then all linked articles are returned with metadata.

#### AC5 — Cannot link non-Published article

Given a Draft article, when an agent attempts to link it, then the request is rejected.

## SQL tables

`TicketArticleLinks` — join between tickets and KB articles:

```sql
CREATE TABLE [dbo].[TicketArticleLinks] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [TicketId]        UNIQUEIDENTIFIER NOT NULL,
    [ArticleId]       UNIQUEIDENTIFIER NOT NULL,
    [LinkedByAgentId] NVARCHAR(450)    NOT NULL,
    [LinkedAt]        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_TicketArticleLinks] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_TicketArticleLinks_Ticket_Article] UNIQUE ([TicketId], [ArticleId]),
    CONSTRAINT [FK_TicketArticleLinks_Tickets] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[Tickets] ([Id]),
    CONSTRAINT [FK_TicketArticleLinks_KnowledgeArticles] FOREIGN KEY ([ArticleId]) REFERENCES [dbo].[KnowledgeArticles] ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-6.5 | Unit | `LinkArticle_ToTicket_CreatesLinkRecord` | Given ticket and Published article, when linked, then join record exists | LinkRecord exists |
| TC-02 | AC-6.5 | Unit | `LinkArticle_RecordsAgentId` | Given agent links article, when link created, then LinkedByAgentId matches | AgentId matches |
| TC-03 | AC-6.5 | Unit | `LinkArticle_RecordsTimestamp` | Given article linked to ticket, when link created, then LinkedAt is set | LinkedAt is not null |
| TC-04 | AC-6.5 | Unit | `UnlinkArticle_FromTicket_RemovesRecord` | Given a link exists, when unlinked, then record is removed | Count == 0 |
| TC-05 | AC-6.5 | Unit | `LinkArticle_DraftArticle_ThrowsInvalidOperation` | Given a Draft article, when linked, then exception is thrown | InvalidOperationException |
| TC-06 | AC-6.5 | Unit | `ListLinkedArticles_ReturnsAllForTicket` | Given ticket has 3 linked articles, when queried, then 3 returned | Count == 3 |
| TC-07 | AC-6.5 | Integration | `LinkArticleEndpoint_Returns201` | Given valid ticket and article, when POST /api/tickets/{id}/articles, then 201 Created | 201 Created |
| TC-08 | AC-6.5 | Integration | `LinkArticleEndpoint_Returns409_WhenDuplicate` | Given link already exists, when POST again, then 409 Conflict | 409 Conflict |

## Notes

- This link supports deflection metrics: tickets that had article links resolved without further action.
- The unique constraint on (TicketId, ArticleId) prevents duplicate links.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
