# US-302 · Branch Entity + Migration

| Field | Value |
|---|---|
| **Story** | `US-302` |
| **Epic** | [EPIC-12 Platform](../epics/EPIC-12.md) |
| **Feature** | [`FEAT-16` Organisation structure](../delivery-plan.md#feat-16--organisation-structure) |
| **Layer** | Backend |
| **Ships with** | — |
| **Actor** | System |
| **Priority** | P0 |
| **Sprint** | [7 — Organisation structure](../delivery-plan.md#sprint-7-organisation-structure) · Slice S8 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-12.8 |
| **Spec criteria** | AC-13 |
| **Depends on** | — |

## Story

**As a system**, **I want** Branch entities, **so that** users, tickets, and customers are grouped by location.

## Business rules

- No BRD BR-n covers this directly. Branch location grouping.

## Acceptance criteria

#### AC1 — Branch entity has required fields (AC-13)

Given a branch is created, when it is stored, then the `Id`, `Name`, `Region`, `Timezone`, and `IsActive` fields are present and correctly typed.

## SQL tables

`Branches` — stores organisational branches / locations:

```sql
CREATE TABLE [dbo].[Branches] (
    [Id]          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Name]        NVARCHAR(200)    NOT NULL,
    [Region]      NVARCHAR(200)    NULL,
    [Timezone]    NVARCHAR(100)    NOT NULL DEFAULT 'UTC',
    [IsActive]    BIT              NOT NULL DEFAULT 1,
    [CreatedAt]   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [UpdatedAt]   DATETIME2        NULL,
    CONSTRAINT [PK_Branches] PRIMARY KEY ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-13 | Unit | `BranchEntityHasRequiredFields` | Given a new Branch entity, when constructed, then `Id`, `Name`, `Region`, `Timezone`, `IsActive` are present with correct defaults | Id is not empty, Name is null, Region is null, Timezone is "UTC", IsActive is true |
| TC-02 | AC-13 | Integration | `BranchCanBePersistedToDatabase` | Given a valid Branch, when saved via EF Core, then it is retrievable with all fields intact | Branch round-trips through the database |

## Notes

- `Timezone` stores an IANA timezone identifier (e.g. `Asia/Riyadh`).
- Follow the existing entity conventions in `CustomerSupport.Domain`.

## Open questions

None.

## Status evidence

Shipped `FEAT-16` — `Branch` entity, same shape as `Department`. See
`docs/superpowers/plans/EPIC-12-US-000-feat-16-organisation-structure/README.md`.

Status is set from what is committed and executed, never from what is planned.
