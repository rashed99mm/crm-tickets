# US-506 · Arabic-Aware Search

| Field | Value |
|---|---|
| **Story** | `US-506` |
| **Epic** | [EPIC-06 Knowledge Base](../epics/EPIC-06-knowledge-base.md) |
| **Feature** | [`FEAT-11` Knowledge base](../delivery-plan.md#feat-11--knowledge-base) |
| **Layer** | Backend |
| **Ships with** | [US-513](./US-513-portal-kb-browse.md) *(frontend)* |
| **Actor** | Customer |
| **Priority** | P0 |
| **Sprint** | [11 — Knowledge base](../delivery-plan.md#sprint-11-knowledge-base) · Slice S4 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-6.4 |
| **Spec criteria** | AC-6.4 |
| **Depends on** | — |

## Story

**As a user**, **I want** to search KB articles in both Arabic and English, **so that** content is findable regardless of language.

## Business rules

- No BRD BR-n covers this directly. Search must handle Arabic diacritics by folding them and Arabic text right-to-left.

## Acceptance criteria

#### AC1 — Arabic diacritic folding (spec AC-6.4)

Given an article containing Arabic text with diacritics, when a search query is made without diacritics, then matching articles are returned.

#### AC2 — English search unaffected

Given an article containing English text, when an English search query is made, then matching articles are returned as before.

#### AC3 — Mixed language query

Given articles in both Arabic and English, when a search query contains both languages, then results include matches from both languages.

#### AC4 — No results returns empty list

Given no articles match the search query, when searched, then an empty list is returned with no error.

## SQL tables

None — search operates on existing `KnowledgeArticles.Title` and `KnowledgeArticles.Body` columns via application-level diacritic folding and SQL full-text search.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-6.4 | Unit | `Search_ArabicWithDiacritics_FoldsAndMatches` | Given article has "كِتَاب", when searching "كتاب", then article is found | Match found |
| TC-02 | AC-6.4 | Unit | `Search_ArabicWithoutDiacritics_Matches` | Given article has "كتاب", when searching "كتاب", then article is found | Match found |
| TC-03 | AC-6.4 | Unit | `Search_English_MatchesNormally` | Given article has "hello world", when searching "hello", then article is found | Match found |
| TC-04 | AC-6.4 | Unit | `Search_MixedLanguage_ReturnsResultsFromBoth` | Given Arabic and English articles exist, when searching, then both are returned | Both present |
| TC-05 | AC-6.4 | Unit | `Search_NoMatch_ReturnsEmptyList` | Given no matching articles, when searching, then empty list returned | Empty list |
| TC-06 | AC-6.4 | Unit | `Search_ArabicPunctuation_Ignored` | Given article has "مرحبا،", when searching "مرحبا", then article is found | Match found |
| TC-07 | AC-6.4 | Integration | `SearchEndpoint_Returns200_WithArabicQuery` | Given Arabic articles exist, when GET /api/kb/search?q=..., then 200 OK | 200 OK |
| TC-08 | AC-6.4 | Integration | `SearchEndpoint_Returns200_WithEnglishQuery` | Given English articles exist, when GET /api/kb/search?q=..., then 200 OK | 200 OK |

## Notes

- Diacritic folding strips Arabic tashkeel (حركات) characters before comparison.
- Consider using SQL Server's built-in full-text search with Arabic collation, or application-level normalization.
- UTF-8 encoding is required throughout the search pipeline.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
