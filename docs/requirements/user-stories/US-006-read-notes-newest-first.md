# US-006 · Read a customer's notes newest first

| Field | Value |
|---|---|
| **Story** | `US-006` *(was `US-1.44`)* — rule proposal: *View Customer Interaction History* (note-level) |
| **Epic** | [EPIC-01 Customer management](../epics/EPIC-01-customer-management.md) |
| **Feature** | [`FEAT-12` Customer notes](../delivery-plan.md#feat-12--customer-notes) |
| **Layer** | Backend |
| **Ships with** | [US-130](./US-130-notes-in-customer-detail.md) *(frontend)* |
| **Actor** | Support Agent |
| **Priority** | P1 |
| **Sprint** | [5 — Notes and attachments](../delivery-plan.md#sprint-5--notes-and-attachments) · Slice S1 |
| **Estimate** | 2 points |
| **Status** | `done` |
| **BRD requirements** | FR-1.12 |
| **Spec criteria** | AC-21 |
| **Depends on** | [US-007](./US-007-record-a-note.md) |

## Story

**As an agent**, **I want** the most recent note at the top, **so that** the current situation is what I read first.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- Notes list newest first and paginated (from AC-21).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Newest first, paginated (spec AC-21)

Given several notes, when listing, then newest first, paginated.

## SQL tables

`CustomerNotes` read path — from the
[S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#customernotes):

```sql
CREATE INDEX IX_CustomerNotes_Customer_Created
    ON [dbo].[CustomerNotes] ([CustomerId], [CreatedAtUtc] DESC);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-21 | Api.IntegrationTests | `planned` | notes created in a known order / `GET` notes page 1 / inspect items | newest first, paged envelope |
| TC-02 | AC-21 | Application.Tests | `planned` | out-of-order inserts / read through the handler / inspect | ordering applied by the query, not the caller |

## Notes

Paginated even though most customers will have few notes, because the one customer who has four hundred is the one whose page will time out.

## Open questions

None.

## Status evidence

Shipped — `GetCustomerNotesQueryHandler`, backed by `CustomerNotesEndpointTests.cs`
(17/17 passing alongside the sibling attachment tests, re-run 2026-08-27). See
`docs/superpowers/plans/EPIC-02-US-001-mvp-customer-workspace-backend/implementation-plan.md` and its
`tasks/task-02-read-the-history.md`. No task-record README exists for this feature (plan-only,
same gap already flagged for other features this session) — this evidence line was written from a
fresh targeted test run, not from a README.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
