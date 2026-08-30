# US-504 · FAQ List Management

| Field | Value |
|---|---|
| **Story** | `US-504` |
| **Epic** | [EPIC-06 Knowledge Base](../epics/EPIC-06-knowledge-base.md) |
| **Feature** | [`FEAT-11` Knowledge base](../delivery-plan.md#feat-11--knowledge-base) |
| **Layer** | Backend |
| **Ships with** | [US-513](./US-513-portal-kb-browse.md) *(frontend)* |
| **Actor** | KB Author |
| **Priority** | P1 |
| **Sprint** | [11 — Knowledge base](../delivery-plan.md#sprint-11-knowledge-base) · Slice S4 |
| **Estimate** | 2 points |
| **Status** | `not started` |
| **BRD requirements** | FR-6.3 |
| **Spec criteria** | AC-6.3 |
| **Depends on** | [US-501](./US-501-kb-publish-archive.md) |

## Story

**As a KB author**, **I want** to curate FAQs from published articles, **so that** common questions are prominent and easy to find.

## Business rules

- No BRD BR-n covers this directly. Only Published articles may be marked as FAQ.
- No BRD BR-n covers this directly. FAQ articles are returned in a dedicated endpoint distinct from the full article list.

## Acceptance criteria

#### AC1 — Mark article as FAQ (spec AC-6.3)

Given a Published article, when the author marks it as FAQ, then the article's IsFaq flag is set to true.

#### AC2 — Unmark article as FAQ

Given a FAQ article, when the author unmarks it, then IsFaq is set to false.

#### AC3 — FAQ endpoint returns only FAQ articles

Given articles exist with IsFaq = true, when the FAQ endpoint is queried, then only FAQ articles are returned.

#### AC4 — Cannot mark non-Published article as FAQ

Given an article in Draft status, when the author attempts to mark it as FAQ, then the request is rejected.

## SQL tables

`KnowledgeArticles` — IsFaq column added to existing table:

```sql
ALTER TABLE [dbo].[KnowledgeArticles] ADD [IsFaq] BIT NOT NULL DEFAULT 0;
```

No new tables required.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-6.3 | Unit | `MarkFaq_PublishedArticle_SetsIsFaqTrue` | Given a Published article, when marked FAQ, then IsFaq == true | IsFaq == true |
| TC-02 | AC-6.3 | Unit | `UnmarkFaq_Article_SetsIsFaqFalse` | Given a FAQ article, when unmarked, then IsFaq == false | IsFaq == false |
| TC-03 | AC-6.3 | Unit | `MarkFaq_DraftArticle_ThrowsInvalidOperation` | Given a Draft article, when marked FAQ, then exception is thrown | InvalidOperationException |
| TC-04 | AC-6.3 | Unit | `GetFaqEndpoint_ReturnsOnlyFaqArticles` | Given 5 articles (2 FAQ), when FAQ endpoint queried, then only 2 returned | Count == 2 |
| TC-05 | AC-6.3 | Integration | `MarkFaqEndpoint_Returns200_WhenPublished` | Given a Published article, when PUT /api/kb/articles/{id}/faq, then 200 OK | 200 OK |
| TC-06 | AC-6.3 | Integration | `GetFaqEndpoint_Returns200_WithFaqList` | Given FAQ articles exist, when GET /api/kb/articles/faq, then 200 OK with list | 200 OK |

## Notes

- IsFaq is a boolean flag on KnowledgeArticles; no separate table needed.
- FAQ ordering could be controlled by a SortOrder column if needed in future.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
