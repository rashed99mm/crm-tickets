# US-133 · Attachments appear on the customer screen

| Field | Value |
|---|---|
| **Story** | `US-133` *(was `US-1.49`)* |
| **Epic** | [EPIC-01 Customer management](../epics/EPIC-01-customer-management.md) |
| **Feature** | [`FEAT-13` Customer attachments](../delivery-plan.md#feat-13--customer-attachments) |
| **Layer** | Frontend |
| **Ships with** | [US-008](./US-008-attach-a-file.md) *(backend)*, [US-131](./US-131-hostile-filename-cannot-escape.md) *(backend)*, [US-132](./US-132-retrieve-and-remove-attachment.md) *(backend)* |
| **Actor** | Support Agent |
| **Priority** | P2 |
| **Sprint** | [5 — Notes and attachments](../delivery-plan.md#sprint-5--notes-and-attachments) · Slice S1 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-1.13 |
| **Spec criteria** | AC-65 |
| **Depends on** | [US-008](./US-008-attach-a-file.md), [US-130](./US-130-notes-in-customer-detail.md) |

## Story

**As an agent**, **I want** to see attached files and upload one where I am already looking at the customer, **so that** I do not need a separate tool.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- On the customer detail view attachments are listed and a file can be uploaded there, with
  client-side size and type checks before submitting (from AC-65).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Listed attachments with checked uploads (spec AC-65)

Given a customer detail view, then attachments are listed and a file can be uploaded, with
client-side size and type checks before submitting.

## SQL tables

None — frontend story. Reads attachments through the API, which joins the
`CustomerAttachments` link to its `Assets` catalogue entry for names, types and sizes
([S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#assets)).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-65 | Frontend (Vitest) | `planned` | customer detail with attachments / inspect DOM | files listed |
| TC-02 | AC-65 | Frontend (Vitest) | `planned` — client-side size check | a file over the limit chosen in the picker / observe | rejected before submit; no request sent |
| TC-03 | AC-65 | Frontend (Vitest) | `planned` — client-side type check | a disallowed type chosen / observe | rejected before submit |

## Notes

Client-side checks are a courtesy that saves a failed upload of a large file; the server checks are the control. This is the first story cut for time, and it is cut together with US-132.

## Open questions

None.

## Status evidence

Shipped — `CustomerAttachmentsComponent` (admin-app), part of the 20/20 combined component-test
run re-confirmed 2026-08-27. See
`docs/superpowers/plans/EPIC-02-US-008-mvp-attachments-frontend/implementation-plan.md`.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
