# US-008 · Attach a file the customer sent

| Field | Value |
|---|---|
| **Story** | `US-008` *(was `US-1.46`)* — rule proposal: *Add Customer Attachment* |
| **Epic** | [EPIC-01 Customer management](../epics/EPIC-01-customer-management.md) |
| **Feature** | [`FEAT-13` Customer attachments](../delivery-plan.md#feat-13--customer-attachments) |
| **Layer** | Backend |
| **Ships with** | [US-133](./US-133-attachments-in-customer-detail.md) *(frontend)* |
| **Actor** | Support Agent |
| **Priority** | P1 |
| **Sprint** | [5 — Notes and attachments](../delivery-plan.md#sprint-5--notes-and-attachments) · Slice S1 |
| **Estimate** | 8 points |
| **Status** | `done` |
| **BRD requirements** | FR-1.13, NFR-7, NFR-8 |
| **Spec criteria** | AC-22, AC-23, AC-24, AC-27 |
| **Depends on** | [US-001](./US-001-create-a-customer.md) *(sprint 2)* |

## Story

**As an agent**, **I want** to store a document a customer supplied, **so that** the evidence sits with the record instead of in my inbox.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- A permitted file within the size limit stores with its metadata — id, original filename, size,
  content type (from AC-22).
- A file over the configured size limit returns 413 and nothing is written to disk; a content type
  outside the allowlist returns 415 and nothing is written (from AC-23, AC-24).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Stored with metadata (spec AC-22)

Given a permitted file within the size limit, then 201 with the stored metadata — id, original
filename, size, content type.

#### AC2 — Oversize writes nothing (spec AC-23)

Given a file over the configured size limit, then 413 and **nothing is written to disk**.

#### AC3 — Disallowed content type refused (spec AC-24)

Given a content type outside the allowlist, then 415 and nothing is written.

#### AC4 — Unknown customer not found (spec AC-27)

Given an unknown customer, then 404.

## SQL tables

Two-row write path — one catalogue entry, one ownership link — full definitions in the
[S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#assets).
File bytes are **not** in the database; they go to `IFileStore` outside the web root:

```sql
-- 1) catalogue: every file enters through [dbo].[Assets]
INSERT targets on [dbo].[Assets]:
    [OriginalFileName] NVARCHAR(260),
    [StoredFileName] NVARCHAR(64) NOT NULL,   -- server-generated GUID
    [ContentType] NVARCHAR(100), [SizeBytes] BIGINT,
    [UploadedById] NVARCHAR(450)

-- 2) link: which customer may read it (AssetId unique among live rows)
INSERT targets on [dbo].[CustomerAttachments]:
    [CustomerId] → Customers, [AssetId] → Assets
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-22 | Api.IntegrationTests | `planned` | a permitted file within the limit / upload against a customer / inspect | 201 with id, original filename, size, content type |
| TC-02 | AC-23 | Api.IntegrationTests | `planned` — nothing written is asserted, not assumed | an oversized file / upload / inspect response + storage directory | 413 code `ERR051`; directory unchanged |
| TC-03 | AC-24 | Api.IntegrationTests | `planned` | a content type outside the allowlist / upload / inspect + storage check | 415 code `ERR052`; nothing written |
| TC-04 | AC-24 (allowlist proof) | Application.Tests | `planned` | each allowed and one disallowed type / evaluate the policy / inspect decisions | allowed set passes, everything else refused |
| TC-05 | AC-27 | Api.IntegrationTests | `planned` | unknown customer in path / upload / observe | 404 |

## Notes

An allowlist, never a blocklist. A blocklist is a list of the attacks someone thought of.

"Nothing is written" in the second criterion means the size cap is checked before the stream is consumed. Checking afterwards means an attacker can fill the disk with files that are then dutifully deleted.

## Open questions

None.

## Status evidence

Shipped — `AddCustomerAttachmentCommandHandler`, `CustomerAttachment` entity, backed by
`CustomerAttachmentEndpointTests.cs` (17/17 passing alongside notes tests, re-run 2026-08-27). See
`docs/superpowers/plans/EPIC-02-US-008-mvp-attachments-backend/implementation-plan.md`. No task-record
README exists for this feature (plan-only) — this evidence line was written from a fresh targeted
test run, not from a README.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
