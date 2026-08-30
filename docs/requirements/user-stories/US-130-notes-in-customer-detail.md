# US-130 · Notes appear on the customer screen

| Field | Value |
|---|---|
| **Story** | `US-130` *(was `US-1.45`)* |
| **Epic** | [EPIC-01 Customer management](../epics/EPIC-01-customer-management.md) |
| **Feature** | [`FEAT-12` Customer notes](../delivery-plan.md#feat-12--customer-notes) |
| **Layer** | Frontend |
| **Ships with** | [US-007](./US-007-record-a-note.md) *(backend)*, [US-006](./US-006-read-notes-newest-first.md) *(backend)* |
| **Actor** | Support Agent |
| **Priority** | P1 |
| **Sprint** | [5 — Notes and attachments](../delivery-plan.md#sprint-5--notes-and-attachments) · Slice S1 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-1.11 |
| **Spec criteria** | AC-62 |
| **Depends on** | [US-007](./US-007-record-a-note.md), [US-128](./US-128-ticket-detail-with-guarded-actions.md) *(sprint 4)* |

## Story

**As an agent**, **I want** to read and add notes where I am already looking at the customer, **so that** the context is in one place.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- On the customer detail view notes are listed newest first, and a note can be added through a
  validated form (from AC-62).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Listed newest first, validated add (spec AC-62)

Given a customer detail view, then notes are listed newest first and a note can be added through a
validated form.

## SQL tables

None — frontend story. Reads and writes `CustomerNotes` through the API
([S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#customernotes)).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-62 | Frontend (Vitest + `HttpTestingController`) | `planned` | customer detail loads / flush notes / inspect DOM | notes listed newest first |
| TC-02 | AC-62 | Frontend (Vitest) | `planned` — validated add | empty body submitted on the note form / inspect | client validation error, no request sent |
| TC-03 | AC-62 | Frontend (Vitest) | `planned` — server errors land on fields | flush a 400 with `errors[]` / inspect DOM | message bound to the body control |

## Notes

Split out from the attachments half of the same screen (US-133) because the two carry different priorities and are cut separately. Bundling a P1 and a P2 in one story means either shipping the P2 unnecessarily or cutting the P1 by accident.

## Open questions

None.

## Status evidence

Shipped — `CustomerNotesComponent` (admin-app), 20/20 combined component tests passing alongside
`CustomerAttachmentsComponent` (re-run 2026-08-27). See
`docs/superpowers/plans/EPIC-02-US-001-mvp-customer-workspace-frontend/README.md`.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
