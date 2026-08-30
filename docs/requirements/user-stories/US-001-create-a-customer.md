# US-001 · Create a customer with validated details

| Field | Value |
|---|---|
| **Story** | `US-001` *(was `US-1.16`)* — rule proposal: *Create Customer* |
| **Epic** | [EPIC-01 Customer management](../epics/EPIC-01-customer-management.md) |
| **Feature** | [`FEAT-03` Customer records](../delivery-plan.md#feat-03--customer-records) |
| **Layer** | Backend |
| **Ships with** | — API-only in S1. The spec defines no frontend criterion for customer management screens - see gap G-5. |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [2 — Customers, ticket capture and queue](../delivery-plan.md#sprint-2--customers-ticket-capture-and-queue) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-1.1, FR-1.2, FR-1.10 |
| **Spec criteria** | AC-7, AC-8 |
| **Depends on** | [US-104](./US-104-field-keyed-validation-errors.md) *(sprint 1)*, [US-112](./US-112-staff-sign-in.md) *(sprint 1)* |

## Story

**As an agent**, **I want** to record a new customer when someone contacts us for the first time,
**so that** their request has somewhere to attach.

## Business rules

- BR-9 — a customer email is unique among records that are not deleted (uniqueness itself is
  US-116's story; this story supplies the normalised value it indexes).
- Derived: name and email are required; email is normalised (trimmed, lower-cased) before
  persistence (from AC-8, and the existing `Email` value object).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Created with location (spec AC-7)

Given a valid name and email, when creating, then 201 with a `Location` header and the created
resource.

#### AC2 — Field-keyed validation (spec AC-8)

Given a missing name, a malformed email, or a field over its length limit, then 400 with errors
keyed by field name.

## SQL tables

`Customers` — full definition in the
[S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#customers). The columns this story writes:

```sql
INSERT targets on [dbo].[Customers]:
    [Id]   UNIQUEIDENTIFIER (Guid v7)  [Name] NVARCHAR(200) NOT NULL,
    [Email] NVARCHAR(320) NOT NULL,     [Phone] NVARCHAR(32) NULL
-- audit fields stamped by the interceptor (US-109), never by the handler
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-7 | Api.IntegrationTests | PASS `AC7_CreateCustomer_ValidRequest_Returns201WithLocation` | valid name + email / `POST /customers` / inspect | 201, `Location` header, created resource in `data`, code `CON010` |
| TC-02 | AC-8 | Api.IntegrationTests | PASS `AC8_CreateCustomer_InvalidFields_Returns400KeyedByField` | name absent / post / inspect | 400 with a `name` field error |
| TC-03 | AC-8 | Api.IntegrationTests | PASS `AC8_CreateCustomer_InvalidFields_Returns400KeyedByField` (same response) | invalid email / post / inspect `errors[]` | 400 keyed to `email` |
| TC-04 | AC-8 | Api.IntegrationTests | PASS `AC8_CreateCustomer_NameOverLengthLimit_Returns400KeyedToName` | name over its limit / post / inspect | 400 keyed to `name` |
| TC-05 | AC-8 | Domain.Tests | PASS `CustomerTests.AC8_Create_Rejects_A_Malformed_Email` — **replaces** the archived `EmailTests` | invalid emails / `Email.Create` / observe | rejected before any persistence |

## Notes

The `Email` value object already exists and normalises on the way in — trimmed and lower-cased — which is what makes the uniqueness rule in US-116 mean what people expect it to mean.

## Open questions

None.

## Status evidence

Implemented in `Features/Customers/Commands/CreateCustomer` and `CustomersController.Create`.

AC-7 -> `AC7_CreateCustomer_ValidRequest_Returns201WithLocation`; AC-8 ->
`AC8_CreateCustomer_InvalidFields_Returns400KeyedByField` and
`AC8_CreateCustomer_NameOverLengthLimit_Returns400KeyedToName`, both in
`tests/CustomerSupport.Tests/Integration/CustomerEndpointTests.cs` against a real LocalDB database.
Entity invariants are covered by `Unit/Domain/CustomerTests.cs`.

Run 2026-08-26: `dotnet test CustomerSupport.slnx` - 192 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
