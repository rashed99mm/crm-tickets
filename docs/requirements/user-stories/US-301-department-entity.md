# US-301 · Department Entity + Migration

| Field | Value |
|---|---|
| **Story** | `US-301` |
| **Epic** | [EPIC-12 Platform](../epics/EPIC-12.md) |
| **Feature** | [`FEAT-16` Organisation structure](../delivery-plan.md#feat-16--organisation-structure) |
| **Layer** | Backend |
| **Ships with** | — |
| **Actor** | System |
| **Priority** | P0 |
| **Sprint** | [7 — Organisation structure](../delivery-plan.md#sprint-7-organisation-structure) · Slice S8 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-12.7 |
| **Spec criteria** | AC-12 |
| **Depends on** | — |

## Story

**As a system**, **I want** Department entities, **so that** users and tickets are grouped by department.

## Business rules

- No BRD BR-n covers this directly. Department grouping.

## Acceptance criteria

#### AC1 — Department entity has required fields (AC-12)

Given a department is created, when it is stored, then the `Id`, `Name`, `ManagerId`, and `IsActive` fields are present and correctly typed.

## SQL tables

`Departments` — stores organisational departments:

```sql
CREATE TABLE [dbo].[Departments] (
    [Id]          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Name]        NVARCHAR(200)    NOT NULL,
    [ManagerId]   UNIQUEIDENTIFIER NULL,
    [IsActive]    BIT              NOT NULL DEFAULT 1,
    [CreatedAt]   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [UpdatedAt]   DATETIME2        NULL,
    CONSTRAINT [PK_Departments] PRIMARY KEY ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-12 | Unit | `DepartmentEntityHasRequiredFields` | Given a new Department entity, when constructed, then `Id`, `Name`, `ManagerId`, `IsActive` are present with correct defaults | Id is not empty, Name is null, ManagerId is null, IsActive is true |
| TC-02 | AC-12 | Integration | `DepartmentCanBePersistedToDatabase` | Given a valid Department, when saved via EF Core, then it is retrievable with all fields intact | Department round-trips through the database |

## Notes

- The `ManagerId` foreign key points to the `Users` table; it is nullable to allow departments without an assigned manager.
- Follow the existing entity conventions in `CustomerSupport.Domain`.

## Open questions

None.

## Status evidence

Shipped `FEAT-16` — `Department` entity (`BaseEntity`, explicit `IsActive`/`Deactivate()`,
matching `Category`'s established lookup-entity pattern). See
`docs/superpowers/plans/EPIC-12-US-000-feat-16-organisation-structure/README.md`.

Status is set from what is committed and executed, never from what is planned.
