# US-305 · Seed Default Department + Branch

| Field | Value |
|---|---|
| **Story** | `US-305` |
| **Epic** | [EPIC-12 Platform](../epics/EPIC-12.md) |
| **Feature** | [`FEAT-16` Organisation structure](../delivery-plan.md#feat-16--organisation-structure) |
| **Layer** | Backend |
| **Ships with** | — |
| **Actor** | System |
| **Priority** | P1 |
| **Sprint** | [7 — Organisation structure](../delivery-plan.md#sprint-7-organisation-structure) · Slice S8 |
| **Estimate** | 2 points |
| **Status** | `done` |
| **BRD requirements** | FR-12.7 |
| **Spec criteria** | AC-16 |
| **Depends on** | [US-301](./US-301-department-entity.md), [US-302](./US-302-branch-entity.md) |

## Story

**As a system**, **I want** a default department and branch seeded, **so that** existing data has a home.

## Business rules

- No BRD BR-n covers this directly. Department grouping.

## Acceptance criteria

#### AC1 — Default department and branch seeded on migration (AC-16)

Given the seed has run, when the `Departments` and `Branches` tables are queried, then at least one row exists in each with a well-known `Id` and `IsActive = true`.

## SQL tables

`Departments` — seeded row:

```sql
INSERT INTO [dbo].[Departments] ([Id], [Name], [ManagerId], [IsActive])
VALUES ('00000000-0000-0000-0000-000000000001', 'General', NULL, 1);
```

`Branches` — seeded row:

```sql
INSERT INTO [dbo].[Branches] ([Id], [Name], [Region], [Timezone], [IsActive])
VALUES ('00000000-0000-0000-0000-000000000001', 'Head Office', NULL, 'UTC', 1);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-16 | Integration | `DefaultDepartmentSeeded` | Given migrations have run, when `Departments` is queried, then the `General` department exists with a deterministic Id | One row with Id = known constant, Name = "General", IsActive = true |
| TC-02 | AC-16 | Integration | `DefaultBranchSeeded` | Given migrations have run, when `Branches` is queried, then the `Head Office` branch exists with a deterministic Id | One row with Id = known constant, Name = "Head Office", Timezone = "UTC", IsActive = true |

## Notes

- Seed uses deterministic GUIDs so downstream features (e.g. US-306) can reference them in tests.
- Seed data is applied via EF Core `HasData` in the entity configuration, not via a separate SQL script.

## Open questions

None.

## Status evidence

Shipped `FEAT-16` — `DepartmentBranchSeeder`, idempotent like `CategorySeeder`, seeding the
well-known-id default department/branch. See
`docs/superpowers/plans/EPIC-12-US-000-feat-16-organisation-structure/README.md`.

Status is set from what is committed and executed, never from what is planned.
