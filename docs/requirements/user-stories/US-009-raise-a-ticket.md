# US-009 · Raise a ticket for a customer's request

| Field | Value |
|---|---|
| **Story** | `US-009` *(was `US-1.21`)* — rule proposal: *Create Ticket* |
| **Epic** | [EPIC-02 Ticket management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-04` Ticket capture](../delivery-plan.md#feat-04--ticket-capture) |
| **Layer** | Backend |
| **Ships with** | [US-127](./US-127-validated-create-ticket-form.md) *(frontend)* |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [2 — Customers, ticket capture and queue](../delivery-plan.md#sprint-2--customers-ticket-capture-and-queue) · Slice S1 |
| **Estimate** | 8 points |
| **Status** | `done` |
| **BRD requirements** | FR-2.1, FR-2.2, FR-2.3, BR-14, BR-15 |
| **Spec criteria** | AC-29, AC-30, AC-31 |
| **Depends on** | [US-001](./US-001-create-a-customer.md) |

## Story

**As an agent**, **I want** to create a ticket with a category and priority, **so that** the request is tracked rather than remembered.

## Business rules

- BR-14 — category from controlled list, no free text (BRD).
- BR-15 — unique stable human-readable reference per ticket (BRD).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Created New with reference (spec AC-29)

Given subject, customer, category and priority, then 201, status `New`, a generated human-readable
reference, and no assignee.

#### AC2 — Field-keyed validation (spec AC-30)

Given a missing subject, an over-length field, or an invalid priority, then 400 keyed by field.

#### AC3 — Unknown referenced resource identified (spec AC-31)

Given an unknown customer or category, then 400 identifying which.

## SQL tables

`Tickets` write path — full definition in the
[S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#tickets):

```sql
INSERT targets on [dbo].[Tickets]:
    [Reference] NVARCHAR(16)  NOT NULL,   -- unique, TKT-nnnnnn
    [Subject]   NVARCHAR(200) NOT NULL,   [Description] NVARCHAR(MAX) NOT NULL,
    [CustomerId] → Customers, [CategoryId] → Categories,
    [Priority] NVARCHAR(16) NOT NULL,     -- string-persisted enum
    [Status]   NVARCHAR(16) NOT NULL      -- 'New' on creation; AssigneeId stays NULL
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-29 | Api.IntegrationTests | PASS `AC29_CreateTicket_ValidRequest_Returns201AsNewAndUnassigned` | valid subject, customer, category, priority / `POST /tickets` / inspect | 201; `Status` = `New`; human-readable `TKT-…` reference; no assignee |
| TC-02 | AC-30 | Api.IntegrationTests | PASS `AC30_CreateTicket_InvalidFields_Returns400KeyedByField` | subject absent / post / inspect | 400 keyed to `subject` |
| TC-03 | AC-30 | Api.IntegrationTests | PASS `AC30_CreateTicket_InvalidFields_…` + `AC30_CreateTicket_SubjectOverLengthLimit_…` | over-length fields and an invalid priority / post / inspect `errors[]` | per-field errors in one response |
| TC-04 | AC-31 | Api.IntegrationTests | PASS `AC31_CreateTicket_UnknownCustomer_Returns400KeyedToCustomerId` | nonexistent `customerId` in body / post / inspect | **400** naming the field (`VAL009`/`VAL010` family), not 404 |
| TC-05 | AC-31 | Api.IntegrationTests | PASS `AC31_CreateTicket_UnknownCategory_Returns400KeyedToCategoryId` | nonexistent category / post / inspect | 400 identifying category |
| TC-06 | AC-29 (reference rule) | Domain.Tests | PASS `AC29_CreateTicket_IssuesUniqueReferences` — **integration, not Domain.Tests**: the sequence is a database object, so format and uniqueness are only observable against a real one. Contiguity is deliberately not asserted | sequence values / generate references / inspect format | `TKT-` + zero-padded sequence, unique |

## Notes

The third criterion is 400 rather than 404 by a stated rule: a resource named in the *path* that does not exist is 404, while a resource referenced in the *body* is a field error, because the addressed collection does exist and the payload is what is wrong. That rule is what makes AC-31 and AC-12 consistent rather than contradictory.

The reference exists because "ticket 4192" is not something a person reads aloud to a customer.

## Open questions

None.

## Status evidence

Implemented in `Features/Tickets/Commands/CreateTicket` and `TicketsController.Create`, over the
`Ticket` aggregate delivered in Phase 0.

AC-29 -> `AC29_CreateTicket_ValidRequest_Returns201AsNewAndUnassigned`,
`AC29_CreateTicket_IssuesUniqueReferences`. AC-30 ->
`AC30_CreateTicket_InvalidFields_Returns400KeyedByField`,
`AC30_CreateTicket_SubjectOverLengthLimit_Returns400KeyedToSubject`. AC-31 ->
`AC31_CreateTicket_UnknownCustomer_Returns400KeyedToCustomerId`,
`AC31_CreateTicket_UnknownCategory_Returns400KeyedToCategoryId`,
`AC31_CreateTicket_BothUnknown_ReportsBothFields`.

BR-14's controlled category list is seeded by `CategorySeeder` and exposed read-only through
`GET /api/Categories`. BR-15's reference comes from a SQL Server sequence: uniqueness and format are
asserted, **contiguity deliberately is not** - `NEXT VALUE FOR` does not join the caller's
transaction, so a rejected create burns a number.

Run 2026-08-26: 192 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
