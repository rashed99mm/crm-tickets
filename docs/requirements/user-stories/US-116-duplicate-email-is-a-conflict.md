# US-116 · A duplicate customer email is a conflict, not a validation error

| Field | Value |
|---|---|
| **Story** | `US-116` *(was `US-1.17`)* |
| **Epic** | [EPIC-01 Customer management](../epics/EPIC-01-customer-management.md) |
| **Feature** | [`FEAT-03` Customer records](../delivery-plan.md#feat-03--customer-records) |
| **Layer** | Backend |
| **Ships with** | — API-only in S1. The spec defines no frontend criterion for customer management screens - see gap G-5. |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [2 — Customers, ticket capture and queue](../delivery-plan.md#sprint-2--customers-ticket-capture-and-queue) · Slice S1 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-1.3, BR-9 |
| **Spec criteria** | AC-9 |
| **Depends on** | [US-001](./US-001-create-a-customer.md), [US-109](./US-109-auditing-and-soft-delete.md) *(sprint 1)* |

## Story

**As an agent**, **I want** a clear refusal when the customer already exists, **so that** I go and find the existing record instead of creating a second one.

## Business rules

- BR-9 — email unique among non-deleted customers (BRD).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Duplicate email is a conflict (spec AC-9)

Given an email already in use, when creating, then **409** naming the conflicting rule — not 400.

## SQL tables

`Customers.Email` uniqueness — from the
[S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#customers):

```sql
CREATE UNIQUE INDEX UX_Customers_Email
    ON [dbo].[Customers] ([Email]) WHERE [IsDeleted] = 0;
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-9 | Api.IntegrationTests | PASS `AC9_CreateCustomer_DuplicateEmail_Returns409NotValidationError` | a customer exists / `POST` another with the same email (any case) / inspect | **409** with code `ERR011`, not 400 |
| TC-02 | AC-9 (contrast) | Api.IntegrationTests | PASS the pair `AC9_…Returns409NotValidationError` (409) vs `AC8_…Returns400KeyedByField` (400) | the same duplicate attempt with an otherwise-valid payload / compare against a malformed-email response | 409 vs 400 — the split is observable |
| TC-03 | AC-9 | Domain.Tests | PASS `CustomerTests.AC9_Email_Is_Lowercased_So_The_Unique_Index_Catches_Case_Variants` — **replaces** the archived `EmailTests` | emails differing by case / compare / — | equal — normalisation is what makes the index catch duplicates |

## Notes

The distinction is deliberate and tested. A duplicate email is not a malformed request: the payload is fine and the state is what refuses it. Getting this wrong makes a client retry with corrected input for a problem no input can fix.

The uniqueness is enforced by a filtered unique index (US-109), not by a read-then-write check, because a check and an insert are two operations and two callers can pass the check simultaneously.

## Open questions

None.

## Status evidence

Implemented as a pre-save existence check plus a `DbUpdateException` catch on
`UX_Customers_Email`, so losing the check-then-insert race still answers 409 rather than 500.

AC-9 -> `AC9_CreateCustomer_DuplicateEmail_Returns409NotValidationError` and
`AC9_CreateCustomer_DuplicateEmailDifferentCase_Returns409` (`CustomerEndpointTests`), plus
`AC9_Email_Is_Lowercased_So_The_Unique_Index_Catches_Case_Variants` (`Unit/Domain/CustomerTests.cs`).

Run 2026-08-26: 192 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
