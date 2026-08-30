# US-509 · KB Admin: Article List

| Field | Value |
|---|---|
| **Story** | `US-509` |
| **Epic** | [EPIC-06 Knowledge Base](../epics/EPIC-06-knowledge-base.md) |
| **Feature** | [`FEAT-11` Knowledge base](../delivery-plan.md#feat-11--knowledge-base) |
| **Layer** | Frontend |
| **Ships with** | [US-501](./US-501-kb-publish-archive.md) *(backend)* |
| **Actor** | KB Author |
| **Priority** | P0 |
| **Sprint** | [11 — Knowledge base](../delivery-plan.md#sprint-11-knowledge-base) · Slice S4 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-6.1 |
| **Spec criteria** | AC-6.1 |
| **Depends on** | [US-501](./US-501-kb-publish-archive.md) |

## Story

**As a KB author**, **I want** to see all articles with status filter, **so that** I can manage the knowledge base efficiently.

## Business rules

- No BRD BR-n covers this directly. The article list shows title, status, category, author, view count, and last updated.
- No BRD BR-n covers this directly. Authors can filter by status (Draft, Published, Archived).

## Acceptance criteria

#### AC1 — Display article list (spec AC-6.1)

Given the KB admin view, when loaded, then a paginated list of all articles is displayed with title, status, category, author, and last updated.

#### AC2 — Filter by status

Given the article list, when the author selects a status filter, then only articles matching that status are shown.

#### AC3 — Empty state

Given no articles match the current filter, when the list is displayed, then an empty state message is shown.

## SQL tables

None — frontend story. Reads from existing `KnowledgeArticles` via backend API.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-6.1 | Unit | `ArticleList_DisplaysAllColumns` | Given articles exist, when list renders, then title, status, category, author, last updated shown | All columns visible |
| TC-02 | AC-6.1 | Unit | `ArticleList_DisplaysPagination` | Given 50 articles, when list renders, then pagination controls shown | Pagination visible |
| TC-03 | AC-6.1 | Unit | `FilterByStatus_ShowsOnlyDrafts` | Given filter set to Draft, when list renders, then only Draft articles shown | Only Draft articles |
| TC-04 | AC-6.1 | Unit | `FilterByStatus_ShowsOnlyPublished` | Given filter set to Published, when list renders, then only Published articles shown | Only Published articles |
| TC-05 | AC-6.1 | Unit | `FilterByStatus_ShowsOnlyArchived` | Given filter set to Archived, when list renders, then only Archived articles shown | Only Archived articles |
| TC-06 | AC-6.1 | Unit | `EmptyState_DisplayedWhenNoResults` | Given no articles match filter, when list renders, then empty state message shown | Empty message visible |
| TC-07 | AC-6.1 | E2E | `KBAdminList_LoadsAndDisplays` | Given user navigates to KB admin, when page loads, then article list is displayed | List with articles |

## Notes

- This story ships with US-501 (backend publish/archive) so the list reflects the complete lifecycle.
- Status filter uses a dropdown or segmented control.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
