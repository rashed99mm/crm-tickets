# US-501 · Dedicated Publish/Archive Commands

| Field | Value |
|---|---|
| **Story** | `US-501` |
| **Epic** | [EPIC-06 Knowledge Base](../epics/EPIC-06-knowledge-base.md) |
| **Feature** | [`FEAT-11` Knowledge base](../delivery-plan.md#feat-11--knowledge-base) |
| **Layer** | Backend |
| **Ships with** | [US-509](./US-509-kb-admin-list.md) *(frontend)* |
| **Actor** | KB Author |
| **Priority** | P0 |
| **Sprint** | [11 — Knowledge base](../delivery-plan.md#sprint-11-knowledge-base) · Slice S4 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-6.1 |
| **Spec criteria** | AC-6.1 |
| **Depends on** | — |

## Story

**As a KB author**, **I want** dedicated publish and archive commands for articles, **so that** the article lifecycle is explicit and auditable.

## Business rules

- No BRD BR-n covers this directly. An article may only be published from Draft status.
- No BRD BR-n covers this directly. An article may be archived from any status except Archived.

## Acceptance criteria

#### AC1 — Publish moves article to Published (spec AC-6.1)

Given an article in **Draft** status, when the author issues the **Publish** command, then the article status transitions to **Published** and the change is recorded in the version history.

#### AC2 — Archive moves article to Archived

Given an article in any status except **Archived**, when the author issues the **Archive** command, then the article status transitions to **Archived** and the change is recorded in the version history.

#### AC3 — Publish from invalid state is rejected

Given an article in **Archived** status, when the author issues the **Publish** command, then the command is rejected with an appropriate error and no state change occurs.

## SQL tables

`KnowledgeArticles` — stores article state (column additions to existing table):

```sql
CREATE TABLE [dbo].[KnowledgeArticles] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [Title]           NVARCHAR(500)    NOT NULL,
    [Slug]            NVARCHAR(500)    NOT NULL,
    [Body]            NVARCHAR(MAX)    NOT NULL,
    [Status]          INT              NOT NULL DEFAULT 0,
    [CategoryId]      UNIQUEIDENTIFIER NULL,
    [AuthorId]        NVARCHAR(450)    NOT NULL,
    [PublishedAt]     DATETIME2        NULL,
    [ArchivedAt]      DATETIME2        NULL,
    [CreatedAt]       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt]       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_KnowledgeArticles] PRIMARY KEY ([Id])
);
```

Status enum: `0` = Draft, `1` = Published, `2` = Archived.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-6.1 | Unit | `Publish_Article_FromDraft_SetsStatusToPublished` | Given an article in Draft, when Publish is called, then status is Published | Status == Published |
| TC-02 | AC-6.1 | Unit | `Publish_Article_FromDraft_RecordsPublishedAt` | Given an article in Draft, when Publish is called, then PublishedAt is set | PublishedAt is not null |
| TC-03 | AC-6.2 | Unit | `Archive_Article_FromDraft_SetsStatusToArchived` | Given an article in Draft, when Archive is called, then status is Archived | Status == Archived |
| TC-04 | AC-6.2 | Unit | `Archive_Article_FromDraft_RecordsArchivedAt` | Given an article in Draft, when Archive is called, then ArchivedAt is set | ArchivedAt is not null |
| TC-05 | AC-6.3 | Unit | `Publish_Article_FromArchived_ThrowsInvalidOperation` | Given an article in Archived, when Publish is called, then exception is thrown | InvalidOperationException |
| TC-06 | AC-6.1 | Integration | `PublishEndpoint_Returns200_WhenArticleExists` | Given a Draft article exists, when POST /api/kb/articles/{id}/publish, then 200 OK with updated article | 200 OK |
| TC-07 | AC-6.3 | Integration | `PublishEndpoint_Returns409_WhenArticleIsArchived` | Given an Archived article exists, when POST /api/kb/articles/{id}/publish, then 409 Conflict | 409 Conflict |

## Notes

- Publish and Archive are separate endpoints, not a generic status-change endpoint, so the contract is explicit.
- `PublishedAt` and `ArchivedAt` are set server-side; the client does not send timestamps.
- The version history table records the transition (see US-502).

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
