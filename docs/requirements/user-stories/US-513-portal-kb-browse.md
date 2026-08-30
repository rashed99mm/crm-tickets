# US-513 · Portal: Browse + Search KB

| Field | Value |
|---|---|
| **Story** | `US-513` |
| **Epic** | [EPIC-06 Knowledge Base](../epics/EPIC-06-knowledge-base.md) |
| **Feature** | [`FEAT-11` Knowledge base](../delivery-plan.md#feat-11--knowledge-base) |
| **Layer** | Frontend |
| **Ships with** | [US-506](./US-506-arabic-search.md) *(backend)*, [US-507](./US-507-article-view-tracking.md) *(backend)*, [US-508](./US-508-helpfulness-vote.md) *(backend)* |
| **Actor** | Customer |
| **Priority** | P0 |
| **Sprint** | [11 — Knowledge base](../delivery-plan.md#sprint-11-knowledge-base) · Slice S4 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-6.6 |
| **Spec criteria** | AC-6.6 |
| **Depends on** | [US-506](./US-506-arabic-search.md), [US-507](./US-507-article-view-tracking.md), [US-508](./US-508-helpfulness-vote.md) |

## Story

**As a customer**, **I want** to browse published articles and search the KB, **so that** I can self-serve answers to common questions.

## Business rules

- No BRD BR-n covers this directly. Only Published articles are visible to customers.
- No BRD BR-n covers this directly. The portal shows article title, summary, category, and helpfulness score.
- No BRD BR-n covers this directly. Search supports both Arabic and English queries.

## Acceptance criteria

#### AC1 — Browse published articles (spec AC-6.6)

Given the KB portal view, when loaded, then a paginated list of Published articles is displayed.

#### AC2 — Article detail view

Given an article in the list, when clicked, then the full article detail is shown with title, body, category, tags, view count, and helpfulness.

#### AC3 — Search returns matching articles

Given the search input, when a query is entered, then matching articles are displayed in relevance order.

#### AC4 — FAQ section visible

Given the KB portal, when loaded, then a dedicated FAQ section shows FAQ articles prominently.

#### AC5 — Helpfulness vote available

Given the article detail view, when displayed, then helpful/unhelpful vote buttons are shown and functional.

#### AC6 — View count incremented on detail

Given the article detail view, when loaded, then the article's view count is incremented.

## SQL tables

None — frontend story. Reads from existing `KnowledgeArticles`, `ArticleViews`, and `ArticleVotes` via backend API.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-6.6 | Unit | `Browse_ShowsOnlyPublishedArticles` | Given articles in all statuses, when portal loads, then only Published shown | Only Published |
| TC-02 | AC-6.6 | Unit | `Browse_DisplaysTitleSummaryCategory` | Given article in list, when rendered, then title, summary, category visible | All fields visible |
| TC-03 | AC-6.6 | Unit | `ArticleDetail_ShowsFullContent` | Given article clicked, when detail loads, then title, body, tags, view count shown | Full content |
| TC-04 | AC-6.6 | Unit | `Search_ReturnsMatchingArticles` | Given query "password reset", when searched, then matching articles shown | Matches shown |
| TC-05 | AC-6.6 | Unit | `Search_SupportsArabicQuery` | Given Arabic query, when searched, then Arabic matches shown | Arabic matches |
| TC-06 | AC-6.6 | Unit | `FaqSection_DisplaysFaqArticles` | Given FAQ articles exist, when portal loads, then FAQ section shown | FAQ section visible |
| TC-07 | AC-6.6 | Unit | `ArticleDetail_VoteButtonsVisible` | Given article detail, when loaded, then helpful/unhelpful buttons shown | Buttons visible |
| TC-08 | AC-6.6 | Unit | `ArticleDetail_IncrementsViewCount` | Given article viewed, when detail loads, then view count incremented | ViewCount++ |
| TC-09 | AC-6.6 | Unit | `EmptyState_ShownWhenNoSearchResults` | Given no matches, when searched, then empty state message shown | Empty message |
| TC-10 | AC-6.6 | E2E | `Portal_BrowseSearchVote_FullFlow` | Given user on portal, when browse, search, view, and vote, then all actions complete | All actions succeed |

## Notes

- This is the main customer-facing KB view; it must be responsive and fast.
- Search results include Arabic diacritic folding (see US-506).
- View tracking (US-507) and helpfulness voting (US-508) are wired into this view.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
