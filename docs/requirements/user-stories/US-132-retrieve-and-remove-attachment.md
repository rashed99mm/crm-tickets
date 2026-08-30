# US-132 · Retrieve and remove an attachment

| Field | Value |
|---|---|
| **Story** | `US-132` *(was `US-1.48`)* |
| **Epic** | [EPIC-01 Customer management](../epics/EPIC-01-customer-management.md) |
| **Feature** | [`FEAT-13` Customer attachments](../delivery-plan.md#feat-13--customer-attachments) |
| **Layer** | Backend |
| **Ships with** | [US-133](./US-133-attachments-in-customer-detail.md) *(frontend)* |
| **Actor** | Support Agent |
| **Priority** | P2 |
| **Sprint** | [5 — Notes and attachments](../delivery-plan.md#sprint-5--notes-and-attachments) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-1.15, FR-1.16 |
| **Spec criteria** | AC-26, AC-28 |
| **Depends on** | [US-008](./US-008-attach-a-file.md) |

## Story

**As an agent**, **I want** to download an attachment and delete one added in error, **so that** the record stays accurate.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- Downloading returns the correct content type and a `Content-Disposition` filename, and requires
  authentication (from AC-26).
- Deleting returns 200 with a confirmation code, soft-deletes the metadata row and removes the file
  from disk (from AC-28).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Authenticated download (spec AC-26)

Given an existing attachment, when downloading, then the correct content type, a
`Content-Disposition` filename, and authentication required.

#### AC2 — Delete soft-deletes row, unlinks file (spec AC-28)

Given an existing attachment, when deleting, then 200 with a confirmation code, the metadata row is
soft-deleted and the file is removed from disk.

## SQL tables

Delete path — link first, then the now-orphaned catalogue entry — from the
[S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#customerattachments):

```sql
-- AC-28 soft-deletes the ownership link…
UPDATE [dbo].[CustomerAttachments]
   SET [IsDeleted]=1, [DeletedAtUtc]=…, [DeletedBy]=…
-- …retires the orphaned [dbo].[Assets] row (freeing its unique StoredFileName),
UPDATE [dbo].[Assets]
   SET [IsDeleted]=1, [DeletedAtUtc]=…, [DeletedBy]=…
-- and the file itself is removed from disk by IFileStore.
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-26 | Api.IntegrationTests | `planned` | an existing attachment / `GET` download with a token / inspect headers + body bytes | correct content type; `Content-Disposition` filename; content matches |
| TC-02 | AC-26 | Api.IntegrationTests | `planned` — authentication half | the same download **without** a token / observe | 401 — never served statically |
| TC-03 | AC-28 | Api.IntegrationTests | `planned` | delete an attachment / inspect response, metadata table (raw), disk | 200 code `CON041`; row soft-deleted; file gone from disk |

## Notes

Files are never served from a static path — a download endpoint streams them after authorising the caller. A static path is authorisation by obscurity, and the URLs end up in browser history and referrer headers.

## Open questions

None.

## Status evidence

Shipped — `DownloadCustomerAttachmentQueryHandler`, `RemoveCustomerAttachmentCommandHandler`,
part of the 17/17 `CustomerAttachmentEndpointTests.cs` run (re-confirmed 2026-08-27). See
`docs/superpowers/plans/EPIC-02-US-008-mvp-attachments-backend/implementation-plan.md`.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
