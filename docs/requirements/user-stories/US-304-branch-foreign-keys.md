# US-304 · Branch Foreign Keys

| Field | Value |
|---|---|
| **Story** | `US-304` |
| **Epic** | [EPIC-12 Platform](../epics/EPIC-12.md) |
| **Feature** | [`FEAT-16` Organisation structure](../delivery-plan.md#feat-16--organisation-structure) |
| **Layer** | Backend |
| **Ships with** | — |
| **Actor** | System |
| **Priority** | P0 |
| **Sprint** | [7 — Organisation structure](../delivery-plan.md#sprint-7-organisation-structure) · Slice S8 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-12.8 |
| **Spec criteria** | AC-15 |
| **Depends on** | [US-302](./US-302-branch-entity.md) |

## Story

**As a system**, **I want** branch assignments, **so that** items are scoped by location.

## Business rules

- No BRD BR-n covers this directly. Branch location grouping.

## Acceptance criteria

#### AC1 — BranchId added to User, Ticket, Customer (AC-15)

Given the `Users`, `Tickets`, and `Customers` tables exist, when the migration is applied, then each table has a nullable `BranchId UNIQUEIDENTIFIER` column with a foreign key to `Branches`.

## SQL tables

`Users` — added column:

```sql
ALTER TABLE [dbo].[Users]
    ADD [BranchId] UNIQUEIDENTIFIER NULL;
ALTER TABLE [dbo].[Users]
    ADD CONSTRAINT [FK_Users_Branches_BranchId]
    FOREIGN KEY ([BranchId]) REFERENCES [dbo].[Branches]([Id]);
```

`Tickets` — added column:

```sql
ALTER TABLE [dbo].[Tickets]
    ADD [BranchId] UNIQUEIDENTIFIER NULL;
ALTER TABLE [dbo].[Tickets]
    ADD CONSTRAINT [FK_Tickets_Branches_BranchId]
    FOREIGN KEY ([BranchId]) REFERENCES [dbo].[Branches]([Id]);
```

`Customers` — added column:

```sql
ALTER TABLE [dbo].[Customers]
    ADD [BranchId] UNIQUEIDENTIFIER NULL;
ALTER TABLE [dbo].[Customers]
    ADD CONSTRAINT [FK_Customers_Branches_BranchId]
    FOREIGN KEY ([BranchId]) REFERENCES [dbo].[Branches]([Id]);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-15 | Integration | `UserHasBranchForeignKey` | Given the migration applied, when a User is queried, then `BranchId` column exists and is nullable | BranchId column present, nullable, typed UNIQUEIDENTIFIER |
| TC-02 | AC-15 | Integration | `TicketHasBranchForeignKey` | Given the migration applied, when a Ticket is queried, then `BranchId` column exists and is nullable | BranchId column present, nullable, typed UNIQUEIDENTIFIER |
| TC-03 | AC-15 | Integration | `CustomerHasBranchForeignKey` | Given the migration applied, when a Customer is queried, then `BranchId` column exists and is nullable | BranchId column present, nullable, typed UNIQUEIDENTIFIER |

## Notes

- All foreign keys are nullable because existing rows have no branch assignment.
- The migration must not drop data; add columns as nullable with a default of NULL.
- Navigation properties should be added to the entity models.

## Open questions

None.

## Status evidence

Shipped `FEAT-16` — nullable `BranchId` is present on `ApplicationUser`, `Ticket`, and `Customer` in
the `Phase2Enrichment` migration. The value is populated from the acting user on customer/ticket
creation and from the target agent on assignment, then consumed by the branch-scoped read handlers.

Status is set from what is committed and executed, never from what is planned.
