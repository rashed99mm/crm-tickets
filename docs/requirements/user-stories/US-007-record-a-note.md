# US-007 · Record a note against a customer

| Field | Value |
|---|---|
| **Story** | `US-007` *(was `US-1.43`)* — rule proposal: *Add Customer Note* |
| **Epic** | [EPIC-01 Customer management](../epics/EPIC-01-customer-management.md) |
| **Feature** | [`FEAT-12` Customer notes](../delivery-plan.md#feat-12--customer-notes) |
| **Layer** | Backend |
| **Ships with** | [US-130](./US-130-notes-in-customer-detail.md) *(frontend)* |
| **Actor** | Support Agent |
| **Priority** | P1 |
| **Sprint** | [5 — Notes and attachments](../delivery-plan.md#sprint-5--notes-and-attachments) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-1.11, BR-6 |
| **Spec criteria** | AC-17, AC-18, AC-19, AC-20 |
| **Depends on** | [US-001](./US-001-create-a-customer.md) *(sprint 2)* |

## Story

**As an agent**, **I want** to note something about a customer, **so that** whoever speaks to them next has the context.

## Business rules

- BR-6 — recorded actor from the authenticated session, never the payload; ignored, not honoured
  (BRD).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Note records author and time (spec AC-17)

Given a body within its length limit, then 201, and the note records its author and creation time.

#### AC2 — Empty body refused (spec AC-18)

Given an empty or whitespace-only body, then 400.

#### AC3 — Author from token only (spec AC-19)

The author is taken from the authenticated token and **never** from the request body. A body
attempting to set an author is ignored, not honoured.

#### AC4 — Unknown customer not found (spec AC-20)

Given an unknown customer, then 404.

## SQL tables

`CustomerNotes` write path — full definition in the
[S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#customernotes):

```sql
INSERT targets on [dbo].[CustomerNotes]:
    [CustomerId] → Customers,  [Body] NVARCHAR(4000) NOT NULL,
    [AuthorId] NVARCHAR(450) NOT NULL   -- from the token, never the body (AC-19)
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-17 | Api.IntegrationTests | `planned` | valid body / `POST` note / inspect response then re-fetch | 201; author and creation time recorded |
| TC-02 | AC-18 | Api.IntegrationTests | `planned` | empty body and a whitespace-only body / post each | 400 for both |
| TC-03 | AC-19 | Application.Tests | `planned` | a request whose body carries an `authorId` / handle with a faked `ICurrentUser` / inspect entity | author from the token; body field ignored, request still succeeds |
| TC-04 | AC-19 | Api.IntegrationTests | `planned` | same attempt over HTTP as another user / re-fetch note / inspect author | attributed to the token's user |
| TC-05 | AC-20 | Api.IntegrationTests | `planned` | unknown customer id in the **path** / post note / observe | 404, code `ERR010` |

## Notes

The third criterion is a per-record authorization rule, not a convenience. Accepting an author from the payload lets any caller attribute their words to anyone — and "ignored, not honoured" is the precise requirement: rejecting the request would also be wrong, because a client sending a harmless extra field should not fail.

## Open questions

None.

## Status evidence

Shipped — `AddCustomerNoteCommandHandler`, backed by `CustomerNotesEndpointTests.cs`
(17/17 passing, re-run 2026-08-27). See `docs/superpowers/plans/EPIC-02-US-001-mvp-customer-workspace-backend/implementation-plan.md`,
`tasks/task-01-add-a-note.md` and `tasks/task-03-author-from-the-session.md`. No task-record
README exists for this feature (plan-only) — this evidence line was written from a fresh targeted
test run, not from a README.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
