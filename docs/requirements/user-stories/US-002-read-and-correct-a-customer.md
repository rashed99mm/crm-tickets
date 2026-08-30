# US-002 · Read and correct a customer's details

| Field | Value |
|---|---|
| **Story** | `US-002` *(was `US-1.19`)* — rule proposal: *View Customer Profile*; secondarily realizes *Update Customer* (US-003) |
| **Epic** | [EPIC-01 Customer management](../epics/EPIC-01-customer-management.md) |
| **Feature** | [`FEAT-03` Customer records](../delivery-plan.md#feat-03--customer-records) |
| **Layer** | Backend |
| **Ships with** | — API-only in S1. The spec defines no frontend criterion for customer management screens - see gap G-5. |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [2 — Customers, ticket capture and queue](../delivery-plan.md#sprint-2--customers-ticket-capture-and-queue) · Slice S1 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-1.6, FR-1.7 |
| **Spec criteria** | AC-12, AC-14 |
| **Depends on** | [US-001](./US-001-create-a-customer.md) |

## Story

**As an agent**, **I want** to open a customer and fix a wrong phone number, **so that** the record stays usable.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- An unknown id answers 404 identically for fetch, update and delete (from AC-12).
- A valid update returns 200 and persists, under the same field validation as creation
  (from AC-14).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Unknown id answers 404 (spec AC-12)

Given an unknown id, when fetching, updating or deleting, then 404.

#### AC2 — Valid update persists (spec AC-14)

Given a valid update, then 200 and the change persists; validation matches AC-8.

## SQL tables

`Customers` update path — from the [S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#customers):

```sql
UPDATE targets on [dbo].[Customers]: [Name], [Email], [Phone]
-- [ModifiedAtUtc]/[ModifiedBy] stamped by the interceptor (FND-23)
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-12 | Api.IntegrationTests | PASS `AC12_GetCustomer_UnknownId_Returns404` | unknown id / `GET /customers/{id}` / observe | 404, code `ERR010` |
| TC-02 | AC-12 | Api.IntegrationTests | PASS `AC12_UpdateCustomer_UnknownId_Returns404` | unknown id / `PUT` with a valid body / observe | 404; nothing written |
| TC-03 | AC-12 | Api.IntegrationTests | PASS `AC12_DeleteCustomer_UnknownId_Returns404` | unknown id / `DELETE` / observe | 404 (completes the trio) |
| TC-04 | AC-14 | Api.IntegrationTests | PASS `AC14_UpdateCustomer_ValidChange_Persists` | valid change to phone / `PUT` / re-fetch | 200, code `CON011`; change persisted on re-fetch |
| TC-05 | AC-14 | Api.IntegrationTests | PASS `AC14_UpdateCustomer_InvalidEmail_Returns400` | an invalid update body / `PUT` / inspect | same field-keyed 400s as creation (one validator) |

## Notes

Update reuses the creation validator rather than defining a parallel set of rules, because two validators for one shape drift, and the drift shows up as a value that can be updated into a record but not created in one.

## Open questions

None.

## Status evidence

Implemented in `GetCustomerByIdQuery` and `UpdateCustomerCommand`.

AC-12 -> `AC12_GetCustomer_UnknownId_Returns404`, `AC12_UpdateCustomer_UnknownId_Returns404`,
`AC12_DeleteCustomer_UnknownId_Returns404`. AC-14 -> `AC14_UpdateCustomer_ValidChange_Persists`,
`AC14_UpdateCustomer_EmailTakenByAnother_Returns409`, `AC14_UpdateCustomer_InvalidEmail_Returns400`.

A soft-deleted customer answers 404 through the global query filter, so "deleted" and "never
existed" are indistinguishable to a caller - the intended outcome for AC-12 and AC-16 both.

Run 2026-08-26: 192 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
