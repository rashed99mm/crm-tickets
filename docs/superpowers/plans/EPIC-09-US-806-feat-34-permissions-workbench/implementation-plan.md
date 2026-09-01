# FEAT-34 Role & Permission Workbench — Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace a role's entire permission set in one transaction — `PUT
/api/admin/permissions/{roleId}` — refusing a save computed from a stale view, refusing to empty a
built-in role, and leaving an audit trail, so the admin screen can stage a set of changes and commit
them as one reviewable action.

**Architecture:** Clean Architecture, four layers, no new project references. The command, its
validator and its handler live in `CustomerSupport.Application`; the transactional write lives in
`CustomerSupport.Infrastructure`'s `PermissionAdministrationService` behind the existing
`IPermissionAdministrationService` port; the endpoint is a fourth action on the existing
`PermissionsController`. No migration — the endpoint writes existing `RolePermissions` rows.

**Tech Stack:** .NET 10, MediatR, FluentValidation, EF Core (SQL Server), xUnit +
`WebApplicationFactory`, FluentAssertions, Moq.

**Spec:** `docs/superpowers/specs/EPIC-09-US-806-permissions-workbench-and-global-confirmation.md`
(`AC-806.1`…`AC-806.10`).

This is the **backend plan**. Per the SDD gate (`CLAUDE.md`) the frontend plan for the same feature
is a separate document — [`frontend-implementation-plan.md`](./frontend-implementation-plan.md) — and
the feature is not shipped until both are implemented.

## Global Constraints

- **Dependency rule.** `Application` references `Domain` only. The transactional write needs
  `AppDbContext` and therefore belongs in `Infrastructure`, reached through
  `IPermissionAdministrationService` (`CustomerSupport.Application/Interfaces/IPermissionAdministrationService.cs:15-20`).
  A handler touching `AppDbContext` is a defect, not a shortcut.
- **No new failure shapes.** Malformed input → 400 with field-keyed `errors[]` via FluentValidation;
  missing entity → 404 via `messages.NotFound<T>`; wrong state → 409 via `messages.Fail<T>(key,
  MessageType.Conflict)`. No new packages.
- **Every new message key needs all four registrations:** `ApplicationErrors` const →
  `SystemCodeMap` entry → `SystemCode` const → `Resources.yaml` en+ar pair. **Free ranges verified
  2026-09-01** against `SystemCode.cs`: last used are `CON078`, `ERR086`, `VAL079`, so this feature
  takes **`CON079`, `ERR087`, `VAL080`, `VAL081`** and nothing else.
- **The built-in-role floor is preserved, not reimplemented.** The role-name set at
  `PermissionAdministrationService.cs:11-21` stays the single source of that rule.
- **Locking is copied, not invented.** `SetAsync` uses the same
  `CreateExecutionStrategy` → transaction → `WITH (UPDLOCK)` shape as `RevokeAsync`
  (`PermissionAdministrationService.cs:83-101`). `EnableRetryOnFailure` forbids bare user
  transactions, so the execution strategy wrapper is mandatory, not stylistic.
- **Every test names its criterion** — `[Trait("AC", "806.1")]` plus a `// AC-806.1` comment, as
  `Integration/PermissionTests.cs:15` already does with comments.
- **Tests are run and their output pasted.** Never "should pass".
- Build clean under warnings-as-errors: `cd backend && dotnet build CustomerSupport.slnx`.
- Both hosts need `ConnectionStrings__DefaultConnection` and `Jwt__Key` or every request 500s
  (`CLAUDE.md`).
- Branch `feat/feat-34-permissions-workbench`; conventional commits, one logical change each.

## File structure (backend)

```
backend/src/CustomerSupport.Application/
  Features/Admin/Commands/SetRolePermissions/
    SetRolePermissionsCommand.cs          NEW  command + request record
    SetRolePermissionsCommandValidator.cs NEW  non-null, non-empty, distinct
    SetRolePermissionsCommandHandler.cs   NEW  result switch → envelope
  Interfaces/IPermissionAdministrationService.cs   MODIFY  SetAsync + StaleSnapshot result
  Errors/ApplicationErrors.cs:133-141              MODIFY  UPDATED, STALE_SNAPSHOT (+ 2 validation keys)
  Messages/SystemCode.cs                           MODIFY  CON079, ERR087, VAL080, VAL081
  Messages/SystemCodeMap.cs:90-95                  MODIFY  map the four new keys
  Behaviors/AuditBehavior.cs:17-38,120-133         MODIFY  audit the command; RoleId fallback

backend/src/CustomerSupport.Api.Shared/
  Localization/Resources.yaml:41-58                 MODIFY  4 new en+ar pairs

backend/src/CustomerSupport.Infrastructure/
  Security/PermissionAdministrationService.cs      MODIFY  SetAsync (transactional set-replace)

backend/src/CustomerSupport.InternalApi/
  Controllers/PermissionsController.cs:44          MODIFY  PUT {roleId:guid}

backend/tests/CustomerSupport.Tests/
  Unit/Features/Admin/PermissionAdministrationTests.cs  MODIFY  handler + validator cases
  Integration/PermissionTests.cs                        MODIFY  endpoint, concurrency, audit
```

## Tasks

Ordered by dependency. Each is one commit and one review gate.

| # | Task | Criteria | Record |
|---|---|---|---|
| 01 | Command, validator, message codes | `AC-806.6` | [`tasks/01-set-role-permissions-contract.md`](./tasks/01-set-role-permissions-contract.md) |
| 02 | `SetAsync` + handler + `StaleSnapshot` | `AC-806.2`…`AC-806.5`, `AC-806.9` | [`tasks/02-atomic-set-service-and-handler.md`](./tasks/02-atomic-set-service-and-handler.md) |
| 03 | Endpoint + integration tests | `AC-806.1`…`AC-806.5`, `AC-806.7`…`AC-806.9` | [`tasks/03-endpoint-and-integration-tests.md`](./tasks/03-endpoint-and-integration-tests.md) |
| 04 | Audit permission changes | `AC-806.10` | [`tasks/04-audit-permission-changes.md`](./tasks/04-audit-permission-changes.md) |

Frontend tasks 05–13 are in [`frontend-implementation-plan.md`](./frontend-implementation-plan.md).

## What this plan deliberately does not do

- **No migration.** `RolePermissions` already has the shape needed
  (`Persistence/Configurations/RolePermissionConfiguration.cs`).
- **No deprecation of the single-mapping endpoints.** `PermissionsController.cs:29-44` stays; spec
  `A8`. Re-expressing them in terms of the batch would change three passing integration tests for
  no user-visible gain.
- **No dynamic authorization policy.** Spec Out of scope — the endpoint keeps
  `[Authorize(Policy = "UserManagement")]` from `PermissionsController.cs:20`.
- **No cross-role atomicity.** Spec `A3`; the frontend reports partial outcomes precisely
  (`AC-806.15`).
