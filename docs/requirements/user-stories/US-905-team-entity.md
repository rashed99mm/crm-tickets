# US-905 · Team Entity

| Field | Value |
|---|---|
| **Story** | `US-905` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-28`](../delivery-plan.md#feat-28) |
| **Layer** | Backend |
| **Ships with** | [US-906](./US-906-lifecycle-timestamps-for-bi.md) *(Backend)* |
| **Actor** | Admin |
| **Priority** | P1 |
| **Sprint** | 17 — Phase 2 workflow |
| **Estimate** | 5 points |
| **Status** | `not started` |

## Story

**As an admin**, **I want** to organise agents into teams inside departments, **so that** the
Org→Branch→Dept→Team→Agent drill-down is real and workload is attributable to a team.

## Business rules

- `Team` mirrors `Department`'s shape: `Name`, `DepartmentId`, `ManagerId`, `IsActive`, unique name
  within its department.
- `ApplicationUser.TeamId` and `Ticket.TeamId` are nullable FKs; a migration backfills to null and
  keeps existing FKs valid.

## Acceptance criteria

#### AC1 — Team entity

Given the organisation, then a `Team` entity exists with `Name`, `DepartmentId`, `ManagerId`,
`IsActive`; CRUD (create/update/deactivate) behaves like `Department`; name is unique per department.

#### AC2 — Team FKs

Given the schema, then `ApplicationUser.TeamId` and `Ticket.TeamId` exist as nullable FKs to `Teams`
and the migration keeps all existing rows valid.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Unit | `Team_Create_WithValidName` | row created |
| TC-02 | AC1 | Unit | `Team_Deactivate_TogglesIsActive` | `IsActive` false |
| TC-03 | AC2 | Integration | `Team_Migration_AddsFks_KeepsRows` | columns exist, seeded rows valid |

## SQL tables

New `Teams`; `AspNetUsers.TeamId`; `Tickets.TeamId`.

## Notes

Seeder adds one default team per existing department so the hierarchy has real rows. The entity and
seeding follow the pattern used for departments exactly.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.