# US-109 · Auditing and soft delete happen without being asked

| Field | Value |
|---|---|
| **Story** | `US-109` *(was `US-1.09`)* |
| **Epic** | [EPIC-12 Platform features](../epics/EPIC-12-platform.md) |
| **Feature** | [`FEAT-01` Platform foundation](../delivery-plan.md#feat-01--platform-foundation) |
| **Layer** | Backend |
| **Ships with** | — Enabler. No user-facing surface, so nothing pairs with it. |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Internal — Data protection owner |
| **Priority** | P0 |
| **Sprint** | [1 — Foundation and authentication](../delivery-plan.md#sprint-1--foundation-and-authentication) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `superseded` |
| **BRD requirements** | NFR-9, BR-8, BR-9 |
| **Spec criteria** | FND-23, FND-24, FND-25, FND-26 |
| **Depends on** | [US-108](./US-108-domain-base-types.md) |

## Story

**As a data protection owner**, **I want** deletion to be non-destructive and every change attributable, enforced by the persistence layer, **so that** neither depends on each handler remembering.

## Business rules

- BR-8 — deleted records are retained, and a deleted customer's email becomes reusable (BRD)
- BR-9 — a customer email is unique among records that are not deleted (BRD)

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Audit fields populated automatically (spec FND-23)

Given any save, when it happens, then auditing fields are populated automatically, not by each
handler.

#### AC2 — Delete marks instead of removing (spec FND-24)

Given a delete on a soft-deletable entity, when executed, then it marks the entity deleted instead
of removing the row.

#### AC3 — Global filter hides deleted rows (spec FND-25)

Given soft-deleted rows, when any query runs, then a global query filter excludes them, applied by
reflection at model build so a new entity cannot be forgotten.

#### AC4 — Filtered unique indexes survive deletion (spec FND-26)

Given unique indexes on soft-deletable entities, when built, then they are **filtered**, so a deleted
record's email becomes reusable while remaining unique among live records.

## SQL tables

Applies to **every soft-deletable table** in the [S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md).
The shared column block and the filtered-index pattern this story is responsible for:

```sql
-- On Customers, Categories, Tickets, CustomerNotes, Assets, CustomerAttachments:
[CreatedAtUtc]  DATETIMEOFFSET NOT NULL,  [CreatedBy]  NVARCHAR(450) NOT NULL,
[ModifiedAtUtc] DATETIMEOFFSET NULL,      [ModifiedBy] NVARCHAR(450) NULL,
[IsDeleted]     BIT            NOT NULL DEFAULT 0,
[DeletedAtUtc]  DATETIMEOFFSET NULL,      [DeletedBy]  NVARCHAR(450) NULL,

-- Every unique index on these tables is filtered (FND-26):
CREATE UNIQUE INDEX UX_Customers_Email
    ON [dbo].[Customers] ([Email]) WHERE [IsDeleted] = 0;
```

`TicketHistory` deliberately has none of the soft-delete columns: it is append-only.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | FND-23 | Application.Tests | ✅ `InterceptorTests.Insert_Stamps_Created_Fields_From_Clock_And_User` | a signed-in user saves a new entity / `SaveChanges` / inspect row | created fields from `IClock`/`ICurrentUser`, not the handler |
| TC-02 | FND-23 | Application.Tests | ✅ `Update_Stamps_Modified_And_Leaves_Created_Alone` | an existing row modified / save / inspect | modified fields set; created fields untouched |
| TC-03 | FND-23 | Application.Tests | ✅ `Anonymous_Actor_Is_Recorded_When_No_User_Is_Signed_In` | no signed-in user / save / inspect actor | recorded as anonymous, never null |
| TC-04 | FND-24 | Application.Tests | ✅ `Remove_Becomes_A_Soft_Delete_And_The_Row_Survives` | remove an entity / save / query raw | state flipped to modified; row physically present |
| TC-05 | FND-25 | Application.Tests | ✅ `Query_Filter_Hides_Deleted_Rows_From_Find_And_Where` | a deleted row / `Find` and `Where` / — | invisible through both paths |
| TC-06 | FND-26 | Api.IntegrationTests | ✅ `FilteredIndexTests.Duplicate_Email_On_A_Live_Row_Is_Rejected_By_The_Database` | two live rows, same email (real SQL Server) / insert second / observe | database rejects — index enforces uniqueness |
| TC-07 | FND-26 | Api.IntegrationTests | ✅ `Email_Of_A_Soft_Deleted_Row_Can_Be_Reused` | delete a customer, reuse its email / insert new row / observe | accepted by the database |
| TC-08 | FND-26 | Api.IntegrationTests | ✅ `Two_Soft_Deleted_Rows_May_Share_An_Email` | two deleted rows, same email / insert both / observe | both accepted |

## Notes

The fourth criterion resolves what looks like a contradiction between US-116 and US-117: a duplicate email must conflict, but a *deleted* customer's email must be reusable. An unfiltered unique index makes those two requirements impossible to satisfy together, and the conflict would surface as a bug against a rule nobody could point at.

## Open questions

- RSK-10 — retired for now: the container-backed database did run on this machine.
  Tracked in [the register](../../product/05-assumptions-and-open-questions.md).

## Status evidence

**Superseded 2026-08-25.** The code that satisfied this story was replaced when the CCE Platform
reference was adopted as the CRM baseline ([ADR-0009](../../adr/0009-adopt-the-support-platform-as-the-crm-baseline.md)).

The criterion it cites is still a valid **requirement**. What is no longer true is that this
codebase meets it: the implementation named in the previous evidence is archived, not running. The
adopted platform may satisfy the same intent by different means, but that has **not been
re-verified**, and carrying a `done` for code that no longer exists would be the exact false claim
this file exists to prevent.

Re-verify against the new baseline, or re-scope the story to the platform equivalent.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
