# US-303 · Department Foreign Keys

| Field | Value |
|---|---|
| **Story** | `US-303` |
| **Epic** | [EPIC-12 Platform](../epics/EPIC-12.md) |
| **Feature** | [`FEAT-16` Organisation structure](../delivery-plan.md#feat-16--organisation-structure) |
| **Layer** | Backend |
| **Ships with** | — |
| **Actor** | System |
| **Priority** | P0 |
| **Sprint** | [7 — Organisation structure](../delivery-plan.md#sprint-7-organisation-structure) · Slice S8 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-12.7 |
| **Spec criteria** | AC-14 |
| **Depends on** | [US-301](./US-301-department-entity.md) |

## Story

**As a system**, **I want** department assignments, **so that** items are grouped by department.

## Business rules

- No BRD BR-n covers this directly. Department grouping.

## Acceptance criteria

#### AC1 — DepartmentId added to User, Ticket, Category (AC-14)

Given the `Users`, `Tickets`, and `Categories` tables exist, when the migration is applied, then each table has a nullable `DepartmentId UNIQUEIDENTIFIER` column with a foreign key to `Departments`.

## SQL tables

`Users` — added column:

```sql
ALTER TABLE [dbo].[Users]
    ADD [DepartmentId] UNIQUEIDENTIFIER NULL;
ALTER TABLE [dbo].[Users]
    ADD CONSTRAINT [FK_Users_Departments_DepartmentId]
    FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Departments]([Id]);
```

`Tickets` — added column:

```sql
ALTER TABLE [dbo].[Tickets]
    ADD [DepartmentId] UNIQUEIDENTIFIER NULL;
ALTER TABLE [dbo].[Tickets]
    ADD CONSTRAINT [FK_Tickets_Departments_DepartmentId]
    FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Departments]([Id]);
```

`Categories` — added column:

```sql
ALTER TABLE [dbo].[Categories]
    ADD [DepartmentId] UNIQUEIDENTIFIER NULL;
ALTER TABLE [dbo].[Categories]
    ADD CONSTRAINT [FK_Categories_Departments_DepartmentId]
    FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Departments]([Id]);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-14 | Integration | `UserHasDepartmentForeignKey` | Given the migration applied, when a User is queried, then `DepartmentId` column exists and is nullable | DepartmentId column present, nullable, typed UNIQUEIDENTIFIER |
| TC-02 | AC-14 | Integration | `TicketHasDepartmentForeignKey` | Given the migration applied, when a Ticket is queried, then `DepartmentId` column exists and is nullable | DepartmentId column present, nullable, typed UNIQUEIDENTIFIER |
| TC-03 | AC-14 | Integration | `CategoryHasDepartmentForeignKey` | Given the migration applied, when a Category is queried, then `DepartmentId` column exists and is nullable | DepartmentId column present, nullable, typed UNIQUEIDENTIFIER |

## Notes

- All foreign keys are nullable because existing rows have no department assignment.
- The migration must not drop data; add columns as nullable with a default of NULL.
- Navigation properties should be added to the entity models.

## Open questions

None.

## Status evidence

Shipped `FEAT-16` — nullable `DepartmentId` is present on the organisation-bearing entities and the
`Phase2Enrichment` migration is applied. `ApplicationUser.AssignOrganisation` is the CQRS/admin
assignment path; ticket assignment inherits the target agent's department/team. Department visibility
is intentionally not enforced because the current policy scopes only users with a persisted `BranchId`.
See
`docs/superpowers/plans/EPIC-12-US-000-feat-16-organisation-structure/README.md`. Department values
are assignment metadata; the current visibility policy is branch-based, not department-based.

Status is set from what is committed and executed, never from what is planned.
