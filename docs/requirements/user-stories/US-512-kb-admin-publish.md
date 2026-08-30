# US-512 · KB Admin: Publish/Archive Actions

| Field | Value |
|---|---|
| **Story** | `US-512` |
| **Epic** | [EPIC-06 Knowledge Base](../epics/EPIC-06-knowledge-base.md) |
| **Feature** | [`FEAT-11` Knowledge base](../delivery-plan.md#feat-11--knowledge-base) |
| **Layer** | Frontend |
| **Ships with** | [US-501](./US-501-kb-publish-archive.md) *(backend)* |
| **Actor** | KB Author |
| **Priority** | P0 |
| **Sprint** | [11 — Knowledge base](../delivery-plan.md#sprint-11-knowledge-base) · Slice S4 |
| **Estimate** | 2 points |
| **Status** | `not started` |
| **BRD requirements** | FR-6.1 |
| **Spec criteria** | AC-6.1 |
| **Depends on** | [US-501](./US-501-kb-publish-archive.md) |

## Story

**As a KB author**, **I want** to publish and archive articles from the UI, **so that** I can manage article lifecycle without leaving the admin view.

## Business rules

- No BRD BR-n covers this directly. Publish button is only enabled for Draft articles.
- No BRD BR-n covers this directly. Archive button is only enabled for non-Archived articles.
- No BRD BR-n covers this directly. Confirmation dialog is shown before Archive action.

## Acceptance criteria

#### AC1 — Publish button visibility (spec AC-6.1)

Given the article list or detail view, when an article is in Draft status, then a Publish button is visible and enabled.

#### AC2 — Archive button visibility

Given the article list or detail view, when an article is not in Archived status, then an Archive button is visible and enabled.

#### AC3 — Publish action calls API

Given a Draft article, when the author clicks Publish, then the publish API is called and the article status updates to Published.

#### AC4 — Archive with confirmation

Given a non-Archived article, when the author clicks Archive, then a confirmation dialog appears before the archive API is called.

#### AC5 — Disabled state for wrong status

Given an article in Archived status, when the list renders, then the Publish and Archive buttons are disabled or hidden.

## SQL tables

None — frontend story. Triggers commands on existing `KnowledgeArticles` via backend API.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-6.1 | Unit | `PublishButton_EnabledForDraftArticle` | Given Draft article row, when rendered, then Publish button enabled | Button enabled |
| TC-02 | AC-6.1 | Unit | `PublishButton_DisabledForPublishedArticle` | Given Published article row, when rendered, then Publish button disabled | Button disabled |
| TC-03 | AC-6.1 | Unit | `ArchiveButton_EnabledForDraftArticle` | Given Draft article row, when rendered, then Archive button enabled | Button enabled |
| TC-04 | AC-6.1 | Unit | `ArchiveButton_DisabledForArchivedArticle` | Given Archived article row, when rendered, then Archive button disabled | Button disabled |
| TC-05 | AC-6.1 | Unit | `ArchiveAction_ShowsConfirmationDialog` | Given non-Archived article, when Archive clicked, then confirmation shown | Dialog visible |
| TC-06 | AC-6.1 | Unit | `PublishAction_CallsApi` | Given Draft article, when Publish clicked, then API call made | API called |
| TC-07 | AC-6.1 | Unit | `ArchiveAction_CallsApiAfterConfirm` | Given confirmation accepted, when Archive confirmed, then API call made | API called |
| TC-08 | AC-6.1 | E2E | `PublishArticle_FromList_UpdatesStatus` | Given Draft article in list, when Publish clicked, then status updates to Published | Status == Published |

## Notes

- Status change triggers a toast notification confirming the action.
- The list refreshes automatically after a status change.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
