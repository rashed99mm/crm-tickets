# S1 database schema — canonical DDL

> **Superseded 2026-08-25 by the platform baseline.** The backend this document describes was
> replaced when the CCE Platform reference was adopted as the CRM baseline — see
> [`EPIC-12-US-000-crm-platform-baseline-design.md`](../specs/EPIC-12-US-000-crm-platform-baseline-design.md).
> The code named below no longer exists in `src/`; it is archived, not deleted. This file is kept
> because it is the record of what was built and why, and deleting it would erase the reasoning
> behind decisions that still hold — the envelope, the localisation approach and the dependency rule
> among them. **Do not follow its steps.**


**Date:** 2026-08-24
**Relates to:** [`EPIC-02-US-016-ticket-lifecycle.md`](./EPIC-02-US-016-ticket-lifecycle.md)
(its *Data model* section) and
[`EPIC-01-US-101-backend-foundation-design.md`](./EPIC-01-US-101-backend-foundation-design.md)
(FND-22–FND-28).

This document elaborates those two specs into concrete T-SQL. **The specs are authoritative**:
if this file disagrees with them, this file is wrong. Story files under
`docs/requirements/user-stories/` quote *excerpts* of these tables and link here; they hold no
definitions of their own.

> **Revision 2026-08-25** — attachment storage split into an `Assets` catalogue plus thin
> ownership links: `CustomerAttachments` no longer carries file metadata; it references
> `Assets` by FK. Rationale: `Assets` is the single point of entry for every file the product
> will store — future surfaces (ticket attachments, chat files) add their own link table and
> never alter the catalogue. Rendered view: [architecture/erd.md](../../architecture/erd.md).

Conventions applied throughout:

- **Audit + soft delete** — every domain table carries `IAuditable`
  (`CreatedAtUtc`, `CreatedBy`, `ModifiedAtUtc`, `ModifiedBy`) and `ISoftDeletable`
  (`IsDeleted`, `DeletedAtUtc`, `DeletedBy`), populated only by the `SaveChanges` interceptor
  (FND-23, FND-24).
- **Filtered unique indexes** — every unique index on a soft-deletable table is
  `WHERE IsDeleted = 0`, so a deleted row's value becomes reusable (FND-26 / ADR 0006).
- **String-persisted enums** — `Status` and `Priority` are `NVARCHAR`, never `INT`: reordering a
  C# enum must not renumber existing rows (spec, *Data model*).
- **Guid v7 ids** — `UNIQUEIDENTIFIER`, time-ordered so clustered inserts do not fragment (F5).
- **No physical deletes** — nothing cascades; the database reinforces what handlers refuse
  (AC-15, assumption A10).

## Entity-to-table map

| Entity | Table | Soft-deletes | Notes |
|---|---|---|---|
| `AppUser` (Identity) | `AspNetUsers` (+ roles, claims, logins, tokens) | no | Identity-owned schema; `DisplayName` added |
| `Customer` | `Customers` | yes | |
| `Category` | `Categories` | yes | Seeded, read-only in S1 |
| `Ticket` | `Tickets` | yes | `ROWVERSION` for optimistic concurrency |
| `TicketHistory` | `TicketHistory` | no | Append-only; no code path updates or deletes it |
| `CustomerNote` | `CustomerNotes` | yes | |
| `Asset` | `Assets` | yes | File catalogue — the single point of entry for every stored file; bytes live outside the DB via `IFileStore`. Added by the 2026-08-25 revision |
| `CustomerAttachment` | `CustomerAttachments` | yes | Ownership link only (`CustomerId` + unique `AssetId`); all file metadata lives in `Assets`. Revised 2026-08-25 |

## AspNetUsers (Identity-managed)

Schema owned by ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`);
listed here because authorization stories depend on its columns, not because we own it.

```sql
-- Created by Identity's migrations. Only the additions below are ours.
ALTER TABLE [dbo].[AspNetUsers] ADD [DisplayName] NVARCHAR(100) NOT NULL DEFAULT N'';
-- Already provided by Identity and relied on by AC-6 lockout:
--   AccessFailedCount INT NOT NULL,
--   LockoutEnd DATETIMEOFFSET NULL
```

Roles (`Agent`, `Supervisor`) live in `AspNetRoles` seeded at startup; membership in
`AspNetUserRoles`.

## Customers

Backs US-001, US-116, US-004, US-002 and US-117 (AC-7…AC-16).

```sql
CREATE TABLE [dbo].[Customers] (
    [Id]            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Customers PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    [Name]          NVARCHAR(200)    NOT NULL,
    [Email]         NVARCHAR(320)    NOT NULL,
    [Phone]         NVARCHAR(32)     NULL,
    -- IAuditable
    [CreatedAtUtc]  DATETIMEOFFSET   NOT NULL,
    [CreatedBy]     NVARCHAR(450)    NOT NULL,
    [ModifiedAtUtc] DATETIMEOFFSET   NULL,
    [ModifiedBy]    NVARCHAR(450)    NULL,
    -- ISoftDeletable
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAtUtc]  DATETIMEOFFSET   NULL,
    [DeletedBy]     NVARCHAR(450)    NULL
);

-- Filtered: a deleted customer's email is reusable (FND-26, AC-9, AC-16).
CREATE UNIQUE INDEX UX_Customers_Email
    ON [dbo].[Customers] ([Email]) WHERE [IsDeleted] = 0;

CREATE INDEX IX_Customers_Name ON [dbo].[Customers] ([Name]);
```

## Categories

Seeded fixed list (assumption A4); referenced by tickets.

```sql
CREATE TABLE [dbo].[Categories] (
    [Id]            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Categories PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    [Name]          NVARCHAR(100)    NOT NULL,
    [IsActive]      BIT              NOT NULL DEFAULT 1,
    [CreatedAtUtc]  DATETIMEOFFSET   NOT NULL,
    [CreatedBy]     NVARCHAR(450)    NOT NULL,
    [ModifiedAtUtc] DATETIMEOFFSET   NULL,
    [ModifiedBy]    NVARCHAR(450)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAtUtc]  DATETIMEOFFSET   NULL,
    [DeletedBy]     NVARCHAR(450)    NULL
);

CREATE UNIQUE INDEX UX_Categories_Name
    ON [dbo].[Categories] ([Name]) WHERE [IsDeleted] = 0;
```

## Tickets

Backs US-009 through US-124 (AC-29…AC-50). `Reference` is the human-readable
`TKT-nnnnnn`; `RowVersion` backs AC-41 concurrency refusal.

```sql
CREATE TABLE [dbo].[Tickets] (
    [Id]            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Tickets PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    [Reference]     NVARCHAR(16)     NOT NULL,
    [Subject]       NVARCHAR(200)    NOT NULL,
    [Description]   NVARCHAR(MAX)    NOT NULL,
    [CustomerId]    UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Tickets_Customer REFERENCES [dbo].[Customers] ([Id]),
    [CategoryId]    UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Tickets_Category REFERENCES [dbo].[Categories] ([Id]),
    [Priority]      NVARCHAR(16)     NOT NULL,
    [Status]        NVARCHAR(16)     NOT NULL,
    [AssigneeId]    NVARCHAR(450)    NULL CONSTRAINT FK_Tickets_Assignee REFERENCES [dbo].[AspNetUsers] ([Id]),
    [RowVersion]    ROWVERSION       NOT NULL,
    [CreatedAtUtc]  DATETIMEOFFSET   NOT NULL,
    [CreatedBy]     NVARCHAR(450)    NOT NULL,
    [ModifiedAtUtc] DATETIMEOFFSET   NULL,
    [ModifiedBy]    NVARCHAR(450)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAtUtc]  DATETIMEOFFSET   NULL,
    [DeletedBy]     NVARCHAR(450)    NULL
);

CREATE UNIQUE INDEX UX_Tickets_Reference ON [dbo].[Tickets] ([Reference]);

-- Queue filters (AC-33): status/priority/customer/assignee, combinable.
CREATE INDEX IX_Tickets_Status_Priority ON [dbo].[Tickets] ([Status], [Priority]);
CREATE INDEX IX_Tickets_CustomerId ON [dbo].[Tickets] ([CustomerId]);
CREATE INDEX IX_Tickets_AssigneeId ON [dbo].[Tickets] ([AssigneeId]);
```

## TicketHistory

Append-only (AC-49): no endpoint or handler updates or removes a row, so the
table has no soft-delete columns — there is nothing a delete could mean here.

```sql
CREATE TABLE [dbo].[TicketHistory] (
    [Id]           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TicketHistory PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    [TicketId]     UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_TicketHistory_Ticket REFERENCES [dbo].[Tickets] ([Id]),
    [ActorId]      NVARCHAR(450)    NOT NULL CONSTRAINT FK_TicketHistory_Actor REFERENCES [dbo].[AspNetUsers] ([Id]),
    [ChangeType]   NVARCHAR(32)     NOT NULL,  -- Created | Assigned | Reassigned | StatusChanged | Reopened
    [FromValue]    NVARCHAR(64)     NULL,
    [ToValue]      NVARCHAR(64)     NULL,
    [OccurredAtUtc] DATETIMEOFFSET  NOT NULL
);

-- Newest-first detail read (AC-50).
CREATE INDEX IX_TicketHistory_Ticket_Occurred
    ON [dbo].[TicketHistory] ([TicketId], [OccurredAtUtc] DESC);
```

## CustomerNotes

Backs US-007, US-006 and US-130 (AC-17…AC-21). `AuthorId` comes from the token, never the
body (AC-19).

```sql
CREATE TABLE [dbo].[CustomerNotes] (
    [Id]            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CustomerNotes PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    [CustomerId]    UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_CustomerNotes_Customer REFERENCES [dbo].[Customers] ([Id]),
    [Body]          NVARCHAR(4000)   NOT NULL,
    [AuthorId]      NVARCHAR(450)    NOT NULL CONSTRAINT FK_CustomerNotes_Author REFERENCES [dbo].[AspNetUsers] ([Id]),
    [CreatedAtUtc]  DATETIMEOFFSET   NOT NULL,
    [CreatedBy]     NVARCHAR(450)    NOT NULL,
    [ModifiedAtUtc] DATETIMEOFFSET   NULL,
    [ModifiedBy]    NVARCHAR(450)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAtUtc]  DATETIMEOFFSET   NULL,
    [DeletedBy]     NVARCHAR(450)    NULL
);

-- Newest-first listing (AC-21).
CREATE INDEX IX_CustomerNotes_Customer_Created
    ON [dbo].[CustomerNotes] ([CustomerId], [CreatedAtUtc] DESC);
```

## Assets

The file catalogue and single point of entry (2026-08-25 revision). Every stored file —
whatever surface attaches it to whatever entity — gets exactly one row here. File bytes are
stored by the `IFileStore` port outside the web root; the database holds metadata only.
`StoredFileName` is server-generated: the original name is never used on disk, so a hostile
filename cannot influence storage location (`US-131`, AC-25).

```sql
CREATE TABLE [dbo].[Assets] (
    [Id]               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Assets PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    [OriginalFileName] NVARCHAR(260)    NOT NULL,
    [StoredFileName]   NVARCHAR(64)     NOT NULL,
    [ContentType]      NVARCHAR(100)    NOT NULL,
    [SizeBytes]        BIGINT           NOT NULL,
    [UploadedById]     NVARCHAR(450)    NOT NULL CONSTRAINT FK_Assets_Uploader REFERENCES [dbo].[AspNetUsers] ([Id]),
    -- IAuditable
    [CreatedAtUtc]  DATETIMEOFFSET   NOT NULL,
    [CreatedBy]     NVARCHAR(450)    NOT NULL,
    [ModifiedAtUtc] DATETIMEOFFSET   NULL,
    [ModifiedBy]    NVARCHAR(450)    NULL,
    -- ISoftDeletable
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAtUtc]  DATETIMEOFFSET   NULL,
    [DeletedBy]     NVARCHAR(450)    NULL
);

-- Server-generated names are unique among live rows; a deleted asset's name is never reused.
CREATE UNIQUE INDEX UX_Assets_StoredFileName
    ON [dbo].[Assets] ([StoredFileName]) WHERE [IsDeleted] = 0;
```

## CustomerAttachments

Ownership link between a customer and an asset (US-008, US-130 and US-133, AC-22–AC-28).
All file metadata lives in [`Assets`](#assets); this table answers only *which customer can see
this file*. One live link per asset — revoking access soft-deletes the link, and the orphaned
catalogue entry is retired with it so its storage name frees up.

```sql
CREATE TABLE [dbo].[CustomerAttachments] (
    [Id]            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CustomerAttachments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    [CustomerId]    UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_CustomerAttachments_Customer REFERENCES [dbo].[Customers] ([Id]),
    [AssetId]       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_CustomerAttachments_Asset REFERENCES [dbo].[Assets] ([Id]),
    -- IAuditable
    [CreatedAtUtc]  DATETIMEOFFSET   NOT NULL,
    [CreatedBy]     NVARCHAR(450)    NOT NULL,
    [ModifiedAtUtc] DATETIMEOFFSET   NULL,
    [ModifiedBy]    NVARCHAR(450)    NULL,
    -- ISoftDeletable
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAtUtc]  DATETIMEOFFSET   NULL,
    [DeletedBy]     NVARCHAR(450)    NULL
);

-- One live link per asset: the same file cannot be attached twice to one customer,
-- and unlinking cannot orphan ambiguity about who may read it.
CREATE UNIQUE INDEX UX_CustomerAttachments_Asset
    ON [dbo].[CustomerAttachments] ([AssetId]) WHERE [IsDeleted] = 0;

CREATE INDEX IX_CustomerAttachments_Customer ON [dbo].[CustomerAttachments] ([CustomerId]);
```

A future surface adds only a link table — `TicketAttachments(TicketId, AssetId)` when its slice
specifies it — with no change to `Assets`.

## What this file does not decide

- Exact `nvarchar` lengths beyond what validators already enforce may shift when the first
  migration is written against real configurations; the migration then becomes the executable
  truth and this file is updated with it.
- Identity's own tables are quoted only where S1 criteria touch them (`AccessFailedCount`,
  `LockoutEnd`, role membership).
- Non-customer link tables into `Assets` (`TicketAttachments`, chat/message attachments): the
  catalogue is designed for them, but each arrives with its own slice's specification.
