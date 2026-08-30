# US-503 · Category/Tag Management

| Field | Value |
|---|---|
| **Story** | `US-503` |
| **Epic** | [EPIC-06 Knowledge Base](../epics/EPIC-06-knowledge-base.md) |
| **Feature** | [`FEAT-11` Knowledge base](../delivery-plan.md#feat-11--knowledge-base) |
| **Layer** | Backend |
| **Ships with** | [US-510](./US-510-kb-admin-create.md) *(frontend)* |
| **Actor** | KB Author |
| **Priority** | P1 |
| **Sprint** | [11 — Knowledge base](../delivery-plan.md#sprint-11-knowledge-base) · Slice S4 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-6.2 |
| **Spec criteria** | AC-6.2 |
| **Depends on** | — |

## Story

**As a KB author**, **I want** to organise articles by category and tag, **so that** content is findable by topic.

## Business rules

- No BRD BR-n covers this directly. Each article belongs to exactly one category.
- No BRD BR-n covers this directly. Each article may have zero or more tags.
- No BRD BR-n covers this directly. Categories are hierarchical (parent-child); tags are flat.

## Acceptance criteria

#### AC1 — Create category (spec AC-6.2)

Given a KB author, when they create a category with name and optional parent, then the category is stored and retrievable.

#### AC2 — Assign article to category

Given an article and a category, when the author assigns the category, then the article's CategoryId is set.

#### AC3 — Add tags to article

Given an article, when the author adds tags, then a many-to-many relationship is created between the article and each tag.

#### AC4 — List categories hierarchy

Given categories exist, when queried, then they are returned as a tree structure with children nested under parents.

#### AC5 — List tags for article

Given an article with tags, when queried, then all associated tags are returned.

## SQL tables

`Categories` — hierarchical article categories:

```sql
CREATE TABLE [dbo].[Categories] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [Name]            NVARCHAR(200)    NOT NULL,
    [Slug]            NVARCHAR(200)    NOT NULL,
    [ParentId]        UNIQUEIDENTIFIER NULL,
    [SortOrder]       INT              NOT NULL DEFAULT 0,
    [CreatedAt]       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt]       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Categories_Parent] FOREIGN KEY ([ParentId]) REFERENCES [dbo].[Categories] ([Id])
);
```

`Tags` — flat tag list:

```sql
CREATE TABLE [dbo].[Tags] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [Name]            NVARCHAR(100)    NOT NULL,
    [Slug]            NVARCHAR(100)    NOT NULL,
    [CreatedAt]       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Tags] PRIMARY KEY ([Id])
);
```

`ArticleTags` — many-to-many join:

```sql
CREATE TABLE [dbo].[ArticleTags] (
    [ArticleId]       UNIQUEIDENTIFIER NOT NULL,
    [TagId]           UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_ArticleTags] PRIMARY KEY ([ArticleId], [TagId]),
    CONSTRAINT [FK_ArticleTags_KnowledgeArticles] FOREIGN KEY ([ArticleId]) REFERENCES [dbo].[KnowledgeArticles] ([Id]),
    CONSTRAINT [FK_ArticleTags_Tags] FOREIGN KEY ([TagId]) REFERENCES [dbo].[Tags] ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-6.2 | Unit | `CreateCategory_WithParent_SetsParentId` | Given a parent category exists, when creating a child, then ParentId is set | ParentId matches |
| TC-02 | AC-6.2 | Unit | `CreateCategory_WithoutParent_ParentIdIsNull` | Given no parent, when creating a category, then ParentId is null | ParentId is null |
| TC-03 | AC-6.2 | Unit | `AssignCategory_ToArticle_UpdatesCategoryId` | Given article and category, when assigned, then CategoryId is set | CategoryId matches |
| TC-04 | AC-6.2 | Unit | `AddTags_ToArticle_CreatesJoinRecords` | Given article and 3 tags, when added, then 3 join records exist | ArticleTags count == 3 |
| TC-05 | AC-6.2 | Unit | `ListCategories_ReturnsTreeHierarchy` | Given parent with 2 children, when queried, then children nested under parent | Tree structure returned |
| TC-06 | AC-6.2 | Unit | `ListTags_ForArticle_ReturnsAllTags` | Given article with 2 tags, when queried, then both tags returned | Tag count == 2 |
| TC-07 | AC-6.2 | Integration | `CreateCategoryEndpoint_Returns201` | Given valid name, when POST /api/kb/categories, then 201 Created | 201 Created |
| TC-08 | AC-6.2 | Integration | `ListCategoriesEndpoint_ReturnsTree` | Given categories exist, when GET /api/kb/categories, then hierarchical list returned | 200 OK with tree |

## Notes

- Category uniqueness is enforced by composite unique index on (Name, ParentId).
- Tag uniqueness is enforced by unique index on Name.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
