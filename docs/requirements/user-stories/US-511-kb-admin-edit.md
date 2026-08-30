# US-511 · KB Admin: Edit Article

| Field | Value |
|---|---|
| **Story** | `US-511` |
| **Epic** | [EPIC-06 Knowledge Base](../epics/EPIC-06-knowledge-base.md) |
| **Feature** | [`FEAT-11` Knowledge base](../delivery-plan.md#feat-11--knowledge-base) |
| **Layer** | Frontend |
| **Ships with** | [US-502](./US-502-article-versioning.md) *(backend)* |
| **Actor** | KB Author |
| **Priority** | P0 |
| **Sprint** | [11 — Knowledge base](../delivery-plan.md#sprint-11-knowledge-base) · Slice S4 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-6.1, FR-6.8 |
| **Spec criteria** | AC-6.1, AC-6.8 |
| **Depends on** | [US-502](./US-502-article-versioning.md) |

## Story

**As a KB author**, **I want** to edit articles and see version history, **so that** I can update content and track changes over time.

## Business rules

- No BRD BR-n covers this directly. Only Draft articles may be edited directly; Published/Archived articles require re-drafting.
- No BRD BR-n covers this directly. Version history is displayed in reverse chronological order.

## Acceptance criteria

#### AC1 — Edit form pre-populated (spec AC-6.1)

Given an existing article, when the edit form loads, then title, body, category, and tags are pre-populated with current values.

#### AC2 — Save creates new version (spec AC-6.8)

Given an article at version N, when the author saves changes, then version becomes N+1 and a version record is created.

#### AC3 — Version history displayed

Given an article with version history, when the edit view loads, then the version list is shown with version number, author, timestamp, and change summary.

#### AC4 — Cannot edit non-Draft directly

Given an article in Published status, when the author opens the edit form, then editing is disabled with a message to re-draft.

## SQL tables

None — frontend story. Reads from existing `KnowledgeArticles` and `ArticleVersions` via backend API.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-6.1 | Unit | `EditForm_PrePopulatesTitle` | Given article with title "Test", when edit form loads, then title field has "Test" | Title matches |
| TC-02 | AC-6.1 | Unit | `EditForm_PrePopulatesBody` | Given article with body content, when edit form loads, then body field has content | Body matches |
| TC-03 | AC-6.8 | Unit | `Save_IncrementsVersion` | Given article at v3, when saved, then version is 4 | Version == 4 |
| TC-04 | AC-6.8 | Unit | `Save_CreatesVersionRecord` | Given article saved, when version record checked, then record exists with metadata | Record exists |
| TC-05 | AC-6.1 | Unit | `VersionHistory_DisplaysInReverseOrder` | Given versions 1,2,3, when history shown, then order is 3,2,1 | Reverse chronological |
| TC-06 | AC-6.1 | Unit | `EditForm_DisabledForPublishedArticle` | Given Published article, when edit form loads, then editing disabled | Fields disabled |
| TC-07 | AC-6.1 | E2E | `EditArticle_SavesAndIncrementsVersion` | Given user edits article, when saved, then version incremented and history shown | Version incremented |

## Notes

- Version history panel is collapsible to save screen space.
- Change summary is auto-generated from changed fields.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
