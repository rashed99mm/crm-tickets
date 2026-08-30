# US-502 · Article Versioning

| Field | Value |
|---|---|
| **Story** | `US-502` |
| **Epic** | [EPIC-06 Knowledge Base](../epics/EPIC-06-knowledge-base.md) |
| **Feature** | [`FEAT-11` Knowledge base](../delivery-plan.md#feat-11--knowledge-base) |
| **Layer** | Backend |
| **Ships with** | [US-511](./US-511-kb-admin-edit.md) *(frontend)* |
| **Actor** | KB Author |
| **Priority** | P1 |
| **Sprint** | [11 — Knowledge base](../delivery-plan.md#sprint-11-knowledge-base) · Slice S4 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-6.8 |
| **Spec criteria** | AC-6.8 |
| **Depends on** | [US-501](./US-501-kb-publish-archive.md) |

## Story

**As a KB author**, **I want** every article change to produce a new version record, **so that** the full history of changes is tracked and auditable.

## Business rules

- No BRD BR-n covers this directly. Each save of an article increments the version number by 1.
- No BRD BR-n covers this directly. The version record captures who made the change, what changed, and when.

## Acceptance criteria

#### AC1 — Save increments version (spec AC-6.8)

Given an article at version N, when the author saves any change, then the article version becomes N+1.

#### AC2 — Version record captures change metadata

Given an article is saved, when the version is created, then a version record is stored containing the author ID, a summary of changed fields, and the timestamp.

#### AC3 — Initial version is 1

Given a new article is created, when it is first saved, then its version is 1.

## SQL tables

`ArticleVersions` — version history per article:

```sql
CREATE TABLE [dbo].[ArticleVersions] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [ArticleId]       UNIQUEIDENTIFIER NOT NULL,
    [VersionNumber]   INT              NOT NULL,
    [AuthorId]        NVARCHAR(450)    NOT NULL,
    [ChangeSummary]   NVARCHAR(1000)   NOT NULL,
    [TitleSnapshot]   NVARCHAR(500)    NOT NULL,
    [BodySnapshot]    NVARCHAR(MAX)    NOT NULL,
    [CreatedAt]       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_ArticleVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ArticleVersions_KnowledgeArticles] FOREIGN KEY ([ArticleId]) REFERENCES [dbo].[KnowledgeArticles] ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-6.8 | Unit | `Save_Article_IncrementsVersionFrom1To2` | Given an article at version 1, when saved, then version is 2 | Version == 2 |
| TC-02 | AC-6.8 | Unit | `Save_Article_IncrementsVersionFromNToNPlus1` | Given an article at version 5, when saved, then version is 6 | Version == 6 |
| TC-03 | AC-6.9 | Unit | `Save_Article_CreatesVersionRecordWithAuthor` | Given an article, when saved by author X, then version record has AuthorId = X | AuthorId matches |
| TC-04 | AC-6.9 | Unit | `Save_Article_CreatesVersionRecordWithTimestamp` | Given an article, when saved, then version record has a CreatedAt timestamp | CreatedAt is not null |
| TC-05 | AC-6.9 | Unit | `Save_Article_CreatesVersionRecordWithChangeSummary` | Given title changed, when saved, then ChangeSummary mentions title | ChangeSummary contains "Title" |
| TC-06 | AC-6.8 | Unit | `Create_Article_SetsInitialVersionTo1` | Given a new article, when created, then version is 1 | Version == 1 |
| TC-07 | AC-6.8 | Integration | `SaveEndpoint_ReturnsUpdatedVersion_WhenArticleExists` | Given article at v1 exists, when PUT /api/kb/articles/{id}, then response version is 2 | Response version == 2 |

## Notes

- Snapshots (TitleSnapshot, BodySnapshot) allow viewing any historical version without reconstructing diffs.
- ChangeSummary is a lightweight textual description; full diffs can be computed client-side from snapshots.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
