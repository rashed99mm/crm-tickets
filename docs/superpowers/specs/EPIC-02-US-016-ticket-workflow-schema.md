# Ticket workflow — data model on the platform baseline

**Date:** 2026-08-25
**Status:** design elaboration. Introduces **no new requirements.**
**Elaborates:** `AC-29`–`AC-50` in
[`EPIC-02-US-016-ticket-lifecycle.md`](./EPIC-02-US-016-ticket-lifecycle.md) and `BASE-11`–
`BASE-14` in
[`EPIC-12-US-000-crm-platform-baseline-design.md`](./EPIC-12-US-000-crm-platform-baseline-design.md).

## Why this document exists

[`EPIC-12-US-000-s1-schema.md`](./EPIC-12-US-000-s1-schema.md) is the canonical DDL for these tables and is
marked **superseded — do not follow its steps**, because the backend it targeted was replaced by the
adopted platform. Its *requirements* survive; its *column conventions do not*. The platform's
`BaseEntity` names audit columns differently and types actor ids differently, so implementing the
old DDL verbatim would produce a schema inconsistent with the six entity types already in the
database.

This file is the reconciliation. It is a design elaboration of already-approved criteria, in the
same relationship to them that `EPIC-12-US-000-s1-schema.md` held — not a new spec, and it invents no
criteria. `BASE-11`–`BASE-14` state that the ticket-lifecycle spec "remains the authority on their
detail"; where this file and that spec disagree on *requirement*, that spec wins. Where they
disagree on *column convention*, the platform wins, and each such point is listed below as a
numbered deviation so none of them is silent.

## Inherited conventions this model must adopt

From `CustomerSupport.Domain.Entities.BaseEntity` and `AppDbContext`, both already shipped:

| Concern | Platform convention | Superseded DDL said |
|---|---|---|
| Identifier | `Guid Id` | same |
| Created | `DateTime CreatedAt`, `Guid? CreatedBy` | `CreatedAtUtc`, `CreatedBy NVARCHAR(450)` |
| Modified | `DateTime? UpdatedAt`, `Guid? UpdatedBy` | `ModifiedAtUtc`, `ModifiedBy` |
| Soft delete | `bool IsDeleted`, `DateTime? DeletedAt` — **no `DeletedBy`** | `IsDeleted`, `DeletedAtUtc`, `DeletedBy` |
| Actor / user id | `Guid` — Identity is `IdentityUser<Guid>` | `NVARCHAR(450)` |
| Soft-delete filter | applied automatically to every `BaseEntity` by `AppDbContext.ApplySoftDeleteQueryFilters` | per-entity `HasQueryFilter` |
| Audit stamping | `AppDbContext.SaveChangesAsync`, which also converts `Deleted` to `Modified` | a separate interceptor |

### Deviations from the superseded DDL, and why

- **D1. Audit column names** follow `BaseEntity` (`CreatedAt`/`UpdatedAt`, not `*Utc`/`Modified*`).
  Consistency with six live entity types beats consistency with a superseded document. Values remain
  UTC; the name simply no longer says so.
- **D2. `DeletedBy` is dropped.** `BaseEntity` has no such column and adding one to five tables would
  fork the base type. Accountability for a deletion is preserved by `UpdatedBy`, which the same
  `SaveChanges` pass stamps on the soft-delete write.
- **D3. Actor ids are `uniqueidentifier`, not `nvarchar(450)`.** The platform's Identity is keyed by
  `Guid`. `TicketHistory.ActorId`, `CustomerNotes.AuthorId`, `Assets.UploadedById` and
  `Tickets.AssigneeId` follow it.
- **D4. `TicketHistory` derives from `BaseEntity` and therefore carries soft-delete columns**, which
  `BR-5` in [`../../architecture/erd.md`](../../architecture/erd.md) wanted *absent* — the absence
  being the enforcement of append-only. The generic `IRepository<T>` is constrained to
  `T : BaseEntity`, so an entity outside that hierarchy needs its own bespoke port and hand-written
  queries. **Append-only is enforced instead by an explicit guard in `AppDbContext.SaveChangesAsync`
  that throws if any `TicketHistory` entry is in `Modified` or `Deleted` state.** This is stronger
  than the original, not weaker: absent columns prevent a soft delete but not an `UPDATE` of
  `ToValue`, whereas the guard refuses both — and unlike absent columns it is directly testable,
  which `AC-49` asks for. Recorded as **ADR-0010**.
- **D5. `OccurredAt` is an explicit column** rather than reusing `CreatedAt`. History rows record
  when a *business event* happened; conflating that with the row's audit timestamp would make the
  two impossible to separate the first time a row is backfilled.

## Entity-to-table map

| Entity | Table | Base type | Soft-deletes |
|---|---|---|---|
| `Customer` | `Customers` | `AggregateRoot` | yes |
| `Category` | `Categories` | `BaseEntity` | yes |
| `Ticket` | `Tickets` | `AggregateRoot` | yes |
| `TicketHistory` | `TicketHistory` | `BaseEntity` | never written — see D4 |
| `CustomerNote` | `CustomerNotes` | `BaseEntity` | yes |
| `Asset` | `Assets` | `BaseEntity` | yes |
| `CustomerAttachment` | `CustomerAttachments` | `BaseEntity` | yes |

## Value objects

Three, all persisted as strings (`nvarchar`), never as `int`. Reordering a C# enum must not renumber
existing rows.

- **`TicketStatus`** — `New`, `Open`, `Assigned`, `In Progress`, `Waiting for Customer`,
  `Waiting for Internal Team`, `Resolved`, `Closed`. Holds the transition table.
- **`TicketPriority`** — `Low`, `Normal`, `High`, `Urgent` (`erd.md` §6; the BRD never enumerated
  these, and that gap is closed there, not here).
- **`TicketChangeType`** — `Created`, `Assigned`, `Reassigned`, `StatusChanged`, `Reopened`.

### The transition table — `AC-37`, `AC-38`, `AC-39`

`New → Open` · `Open → Assigned` · `Open → Resolved` · `Assigned → In Progress` ·
`In Progress → Waiting for Customer` · `In Progress → Waiting for Internal Team` ·
`In Progress → Resolved` · `Waiting for Customer → In Progress` ·
`Waiting for Internal Team → In Progress` · `Resolved → In Progress` ·
`Resolved → Closed` · `Closed → In Progress`.

Everything else is refused, **including every self-transition** (`AC-39`, `BR-4`). The table lives in
`TicketStatus.CanTransitionTo`, consulted by `Ticket.ChangeStatus`. `Ticket.Status` has a private
setter: a public one would let a handler bypass the table, and eventually one would.

A refused transition is a **conflict**, not a validation failure (`AC-38`): the request is
well-formed and the state is wrong. `Ticket.ChangeStatus` throws `InvalidOperationException`, which
the Application layer maps to `ErrorType.Conflict` → 409 with `ERR021`/`ERR022`.

## Tables

Columns below are additional to the `BaseEntity` set (`Id`, `CreatedAt`, `CreatedBy`, `UpdatedAt`,
`UpdatedBy`, `IsDeleted`, `DeletedAt`), which every table carries.

### Customers — `AC-7`…`AC-16`

| Column | Type | Notes |
|---|---|---|
| `Name` | `nvarchar(200)` not null | |
| `Email` | `nvarchar(320)` not null | |
| `Phone` | `nvarchar(32)` null | |

- `UX_Customers_Email` unique on `Email` **`WHERE IsDeleted = 0`** — a deleted customer's email
  becomes reusable (`AC-9`, `AC-16`, ADR-0006).
- `IX_Customers_Name` for the `AC-13` search.

### Categories — assumption `A4`

| Column | Type | Notes |
|---|---|---|
| `Name` | `nvarchar(100)` not null | |
| `IsActive` | `bit` not null default 1 | |

- `UX_Categories_Name` unique on `Name` `WHERE IsDeleted = 0`.
- Seeded fixed list, read-only in S1.

### Tickets — `AC-29`…`AC-47`

| Column | Type | Notes |
|---|---|---|
| `Reference` | `nvarchar(16)` not null | `TKT-nnnnnn` |
| `Subject` | `nvarchar(200)` not null | |
| `Description` | `nvarchar(max)` not null | |
| `CustomerId` | `uniqueidentifier` not null FK → `Customers` | no cascade |
| `CategoryId` | `uniqueidentifier` not null FK → `Categories` | no cascade |
| `Priority` | `nvarchar(16)` not null | string-persisted enum |
| `Status` | `nvarchar(16)` not null | string-persisted enum |
| `AssigneeId` | `uniqueidentifier` null FK → `AspNetUsers` | null = unassigned (`AC-29`) |
| `RowVersion` | `rowversion` | `AC-41` optimistic concurrency |

- `UX_Tickets_Reference` unique on `Reference`, **unfiltered** — the stated exception to the
  filtered-unique convention. A reference read aloud to a customer must never be reissued, so a soft
  delete does not free it.
- `IX_Tickets_Status_Priority`, `IX_Tickets_CustomerId`, `IX_Tickets_AssigneeId` back the `AC-33`
  filters, which combine.

**Reference generation.** A SQL Server sequence, `TicketReferenceSequence`, starting at 1000 and
incrementing by 1; the reference is `TKT-` plus the value zero-padded to six digits. A sequence
rather than `MAX(Reference) + 1` because the latter races under concurrent inserts and the unique
index would turn that race into a 500. The sequence is created by the same migration as the tables;
the port that reads it is `ITicketReferenceGenerator`, declared in `Domain/Interfaces` beside
`IAuditService` and implemented in `Infrastructure` — the Domain must not know about SQL Server.

### TicketHistory — `AC-48`…`AC-50`

| Column | Type | Notes |
|---|---|---|
| `TicketId` | `uniqueidentifier` not null FK → `Tickets` | |
| `ActorId` | `uniqueidentifier` not null FK → `AspNetUsers` | from the token, never the payload (`BR-6`) |
| `ChangeType` | `nvarchar(32)` not null | the five values above |
| `FromValue` | `nvarchar(64)` null | null on `Created` |
| `ToValue` | `nvarchar(64)` null | |
| `OccurredAt` | `datetime2` not null | D5 |

- `IX_TicketHistory_Ticket_Occurred` on (`TicketId`, `OccurredAt` DESC) for the newest-first read
  (`AC-50`).
- Append-only, enforced as described in **D4**.

### CustomerNotes — `AC-17`…`AC-21`

| Column | Type | Notes |
|---|---|---|
| `CustomerId` | `uniqueidentifier` not null FK → `Customers` | |
| `Body` | `nvarchar(4000)` not null | |
| `AuthorId` | `uniqueidentifier` not null FK → `AspNetUsers` | from the token (`AC-19`) |

- `IX_CustomerNotes_Customer_Created` on (`CustomerId`, `CreatedAt` DESC) — `AC-21`.

### Assets — `AC-22`…`AC-28`

The single point of entry for every stored file. Bytes live outside the database behind `IFileStore`.

| Column | Type | Notes |
|---|---|---|
| `OriginalFileName` | `nvarchar(260)` not null | metadata only, never touches the filesystem |
| `StoredFileName` | `nvarchar(100)` not null | server-generated GUID name (`AC-25`) |
| `ContentType` | `nvarchar(100)` not null | allowlisted (`AC-24`) |
| `SizeBytes` | `bigint` not null | capped (`AC-23`) |
| `UploadedById` | `uniqueidentifier` not null FK → `AspNetUsers` | |

- `UX_Assets_StoredFileName` unique `WHERE IsDeleted = 0`.

### CustomerAttachments

Ownership link only. All file metadata lives in `Assets`, so a future `TicketAttachments` reuses the
catalogue rather than altering it.

| Column | Type | Notes |
|---|---|---|
| `CustomerId` | `uniqueidentifier` not null FK → `Customers` | |
| `AssetId` | `uniqueidentifier` not null FK → `Assets` | |

- `UX_CustomerAttachments_Asset` unique on `AssetId` `WHERE IsDeleted = 0` — one live link per asset.
- `IX_CustomerAttachments_Customer` on `CustomerId`.

## No cascades

Every FK is `DeleteBehavior.Restrict`. Nothing in this schema cascades: the database reinforces what
handlers already refuse (`AC-15`, assumption `A10`, `BR-8`). Application code issues no `DELETE`.

## What this document does not decide

Handlers, endpoints, DTOs, validators and the error-code mapping. Those belong to the feature plans
that cite `AC-29`–`AC-50`, and each arrives with its own failing test first.
