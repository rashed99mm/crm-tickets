# US-804 · Permission Entity + Role Mapping

| Field | Value |
|---|---|
| **Story** | `US-804` |
| **Epic** | [EPIC-09 Security & Administration](../epics/EPIC-09-administration.md) |
| **Feature** | [`FEAT-21` Security & Administration](../delivery-plan.md#feat-21--security-administration) |
| **Layer** | Backend |
| **Ships with** | [US-805](./EPIC-09-US-805-permission-admin-ui.md) *(frontend)* |
| **Actor** | System |
| **Priority** | P1 |
| **Sprint** | [12 — Administration](../delivery-plan.md#sprint-12-administration) · Slice S9 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-10.8 |
| **Spec criteria** | AC-804 |
| **Depends on** | [US-301](./US-301-department-entity.md) |

## Story

**As a system**, **I want** granular permissions with role mapping, **so that** access control is fine-grained and not limited to role names alone.

## Business rules

- No BRD BR-n covers this directly. Permissions are granular actions (e.g. `ticket.create`, `ticket.assign`, `report.export`).
- No BRD BR-n covers this directly. Roles contain a set of permissions; users inherit permissions through their role.
- No BRD BR-n covers this directly. Permission checks are enforced at the API endpoint level via authorization policies.

## Acceptance criteria

#### AC1 — Permission entity (spec AC-804)

Given the domain model, when `Permission` entity is defined, then it has `id`, `name` (e.g. `ticket.create`), and `description`.

#### AC2 — Role-Permission mapping (spec AC-804)

Given the domain model, when `RolePermission` join entity is defined, then it maps roles to permissions in a many-to-many relationship.

#### AC3 — Permission seeded (spec AC-804)

Given the system starts, when seeding runs, then all defined permissions are created and mapped to their default roles.

## SQL tables

New `Permissions` and `RolePermissions` tables.

```sql
CREATE TABLE [dbo].[Permissions] (
    [Id]          UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [Name]        NVARCHAR(100)    NOT NULL,
    [Description] NVARCHAR(500)    NULL,
    [CreatedAt]   DATETIME2        NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_Permissions_Name] UNIQUE ([Name])
);

CREATE TABLE [dbo].[RolePermissions] (
    [RoleId]       UNIQUEIDENTIFIER NOT NULL,
    [PermissionId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
    CONSTRAINT [FK_RolePermissions_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles] ([Id]),
    CONSTRAINT [FK_RolePermissions_Permissions] FOREIGN KEY ([PermissionId]) REFERENCES [dbo].[Permissions] ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-804 | Unit | `PermissionEntityHasRequiredFields` | Given the Permission entity, when inspected, then it has id, name, and description fields. | All fields present |
| TC-02 | AC-804 | Integration | `RolePermissionMappingWorks` | Given a role with `ticket.create` permission, when the mapping is queried, then the permission is associated with the role. | Mapping correct |
| TC-03 | AC-804 | Integration | `PermissionsSeededOnStartup` | Given the system starts, when seeding completes, then at least 10 default permissions exist. | Seed data created |

## Notes

Permission names follow a `resource.action` convention. Default roles (Admin, Manager, Supervisor, Agent) are pre-mapped. Lives in Domain layer.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
