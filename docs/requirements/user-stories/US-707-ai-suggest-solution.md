# US-707 · Suggest KB Solutions

| Field | Value |
|---|---|
| **Story** | `US-707` |
| **Epic** | [EPIC-11 AI Features](../epics/EPIC-11-ai.md) |
| **Feature** | [`FEAT-20` AI-Powered Features](../delivery-plan.md#feat-20--ai-powered-features) |
| **Layer** | Backend |
| **Ships with** | [US-702](./US-702-ai-service-port.md) *(backend)* |
| **Actor** | Agent |
| **Priority** | P2 |
| **Sprint** | [15 — AI assist](../delivery-plan.md#sprint-15-ai-assist) · Slice S7 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-7.4 |
| **Spec criteria** | AC-707 |
| **Depends on** | [US-702](./US-702-ai-service-port.md), [US-703](./US-703-ai-human-gate.md) |

## Story

**As an agent**, **I want** AI-suggested KB articles, **so that** I find solutions faster and provide consistent answers.

## Business rules

- No BRD BR-n covers this directly. KB suggestions are ranked by relevance score and limited to top 3 results.
- No BRD BR-n covers this directly. Suggestions include a link to the KB article for quick reference.

## Acceptance criteria

#### AC1 — Suggest solutions endpoint (spec AC-707)

Given a ticket with a description, when the agent requests KB suggestions, then the endpoint returns up to 3 relevant KB article references with titles and URLs.

#### AC2 — Suggestions displayed in sidebar (spec AC-707)

Given suggestions are returned, when the agent is on the ticket detail page, then suggested KB articles appear in a sidebar panel.

#### AC3 — Agent can view KB article (spec AC-707)

Given a suggested KB article, when the agent clicks the link, then the KB article opens in a new tab or modal.

## SQL tables

No new tables. Queries existing `Contents` (knowledge base) table.

```sql
-- KB suggestion query (semantic search or keyword match)
SELECT TOP 3 [c].[Id], [c].[Title], [c].[Slug], [c].[Body]
FROM [dbo].[Contents] [c]
WHERE [c].[Status] = 'published'
  AND (CONTAINS([c].[Body], @searchTerms) OR CONTAINS([c].[Title], @searchTerms))
ORDER BY relevance DESC;
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-707 | Integration | `SuggestSolutionsReturnsArticles` | Given KB articles about password reset, when the agent requests suggestions for a password ticket, then at least 1 relevant article is returned. | Relevant KB article returned |
| TC-02 | AC-707 | Component | `SuggestionsDisplayInSidebar` | Given suggestions are returned, when the ticket detail renders, then the sidebar shows up to 3 KB articles. | Sidebar panel with articles |
| TC-03 | AC-707 | Component | `SuggestionLinkOpensArticle` | Given a suggested article, when the agent clicks it, then the KB article is displayed. | Article view opens |

## Notes

Uses the existing `Contents` (knowledge base) table. Semantic search implementation depends on the provider capabilities. Frontend: sidebar panel on the ticket detail view. Uses `AgentsWorkspace.html` mockup as reference.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
