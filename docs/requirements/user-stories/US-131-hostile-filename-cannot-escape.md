# US-131 · A hostile filename cannot escape the storage directory

| Field | Value |
|---|---|
| **Story** | `US-131` *(was `US-1.47`)* |
| **Epic** | [EPIC-01 Customer management](../epics/EPIC-01-customer-management.md) |
| **Feature** | [`FEAT-13` Customer attachments](../delivery-plan.md#feat-13--customer-attachments) |
| **Layer** | Backend |
| **Ships with** | [US-133](./US-133-attachments-in-customer-detail.md) *(frontend)* |
| **Actor** | Internal — Security reviewer |
| **Priority** | P1 |
| **Sprint** | [5 — Notes and attachments](../delivery-plan.md#sprint-5--notes-and-attachments) · Slice S1 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-1.14 |
| **Spec criteria** | AC-25 |
| **Depends on** | [US-008](./US-008-attach-a-file.md) |

## Story

**As a security reviewer**, **I want** uploads confined to the configured directory, **so that** a crafted filename cannot write outside it.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- Filenames containing path separators or traversal sequences cannot place the stored file outside
  the configured directory; the stored name is server-generated and the original is metadata only
  (from AC-25).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Traversal filename confined (spec AC-25)

Given a filename containing path separators or traversal sequences (`../`, `..\`), then the stored
path stays inside the configured directory. The stored name is server-generated; the original is
metadata only.

## SQL tables

No new table. The column that makes this work is
`Assets.StoredFileName` — server-generated, so the hostile name never reaches the filesystem;
the attachment row only links to the catalogue entry
([S1 schema, Assets](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#assets)).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-25 | Application.Tests (fake `IFileStore`) or Infrastructure unit test | `planned` — parameterised over `../`, `..\`, absolute paths, null bytes, reserved names | a hostile original filename / store / inspect resolved path + stored name | path stays under the configured root; stored name is a server GUID |
| TC-02 | AC-25 | Api.IntegrationTests | `planned` — end to end with real storage | upload with a traversal filename / inspect disk + metadata row | file written inside the root only; row records the original as metadata |

## Notes

The server-generated name is the actual defence — the original filename never touches the filesystem, so there is nothing to sanitise. Asserting the resolved path sits under the root before writing is the second layer, for the case where someone later reintroduces the original name.

## Open questions

None.

## Status evidence

Shipped — proven directly by
`AC25_Upload_HostileFilename_StoresAGuidInsideTheRoot` (`CustomerAttachmentEndpointTests.cs`:
uploads `../../etc/passwd` as the claimed filename and asserts the stored path never contains it),
part of the 17/17 passing run re-confirmed 2026-08-27. See
`docs/superpowers/plans/EPIC-02-US-008-mvp-attachments-backend/implementation-plan.md`.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
