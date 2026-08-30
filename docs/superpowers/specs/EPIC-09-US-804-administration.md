# Administration — audit log query/viewer and platform settings UI

**Sprint:** 12 · **Feature:** `FEAT-21` · **Stories:** `US-801`, `US-802`, `US-803` ·
**Epic:** `EPIC-09` Security & Administration

## Problem

`AuditLog`, `IAuditService` and `PlatformSettingsController` already exist (inherited platform), but
nothing lets an admin see either through the product: there is no query endpoint or screen for the
audit trail, and no settings screen despite the backend being complete. Investigated first, and worth
recording: `IAuditService` was registered in DI but **never called** — `AuditBehavior`, the pipeline
component whose name implies it writes audit entries, only logged to the application log
(`ILogger.LogDebug`). `AuditLogs` has been permanently empty since this platform was adopted.

## Assumptions

A1. **`AuditBehavior` is fixed as a prerequisite, not treated as in-scope story work.** Building a
    query endpoint and a viewer over a table nothing ever populates would ship two working layers
    over dead data. The fix is generic across the behavior's 11 already-designated auditable
    commands (`CreateUserCommand`, `UpdatePlatformSettingCommand`, etc.) — it does not audit tickets,
    customers, or anything else; broadening `AuditableCommands` is a separate decision this pass does
    not make.

A2. **No "before" snapshot (`AuditLog.OldValues` stays null).** The generic pipeline behavior sees
    only the request and the response — there is no read-before-write step to diff against. `NewValues`
    is the request payload itself (best effort, not a true diff). Building real old/new diffing would
    mean a pre-handler read per auditable command, which is real scope this pass does not take on.

A3. **`US-803` (platform settings UI) is frontend-only**, per the story's own notes — the backend
    (`PlatformSettingsController`, full CRUD) already exists. Audit logging of settings changes
    (`US-803` `AC4`) is satisfied by A1's fix, since `UpdatePlatformSettingCommand` is already in
    `AuditableCommands`.

A4. **`US-804`/`US-805` (permission entity + admin UI) are not this slice.** A genuinely separate,
    larger capability (a new authorization model layered over the existing role system) — deferred.

A5. **Audit log filtering: `actionType` and `userId` only** (matches `US-801`'s own AC2/AC3). No
    date-range filter on the backend this pass, even though `US-802`'s AC2 mentions one — the viewer
    filters what the query supports; a date-range query parameter is a small enough addition to fold
    in only if time allows, not a hard requirement of this spec.

## Out of scope

- `US-804`, `US-805` — permissions (A4).
- Broadening which commands are audited beyond the existing 11 (A1).
- Old/new value diffing (A2).
- Date-range filtering on the audit query (A5) — may be added if time allows, not committed.

## Acceptance criteria

AC-140. Given an admin calls `GET /api/admin/audit-log`, then a paginated list of audit entries is
returned, newest first.

AC-141. Given `actionType=Created` is supplied, then only entries whose `Action` equals `Created` are
returned.

AC-142. Given `userId={guid}` is supplied, then only entries whose `UserId` equals that value are
returned.

AC-143. Given a non-admin caller, when calling the audit-log endpoint, then the response is `403`.

AC-144. Given an auditable command (e.g. `CreateUserCommand`) succeeds, then an `AuditLog` row is
persisted with the correct `Action`, `EntityType`, `EntityId` and `UserId` (proves the `AuditBehavior`
fix, A1).

AC-145. Given the same command fails validation, then no `AuditLog` row is written.

AC-146 (frontend). Given an admin navigates to the audit log screen, then a table of entries is
shown, newest first, with filter controls for action type and user, and pagination.

AC-147 (frontend). Given an admin navigates to platform settings, then a table/form of existing
settings is shown, editable, with server validation errors surfaced per field.

## Design

### Backend: Application

**New:** `Features/Admin/Dtos/AuditLogDto.cs`, `Features/Admin/Queries/GetAuditLog/` —
`GetAuditLogQuery : BasePagedQuery` with `ActionType`/`UserId` filters,
`IQueryHandler` projecting `AuditLog` newest-first via `IRepository<AuditLog>.GetPagedAsync`.

**Edit:** `AuditBehavior<TRequest,TResponse>` — calls `IAuditService.LogAsync` (A1/A2, detailed above).

### Backend: API

**New:** `AdminController` (or an `AuditLog` action added to an existing admin-scoped controller) —
`GET /api/admin/audit-log`, `[Authorize(Policy = "Admin")]`.

### Frontend

**New:** `AuditLogComponent` — table (action, entity type, user, timestamp), action-type and user
filter controls, pagination, row-click detail (old/new values, IP, user agent — even though
`OldValues` is always null this pass, the column exists so it's not a second migration when A2 is
revisited).

**New:** `PlatformSettingsComponent` — list existing settings (key, value, category, description),
inline or per-row edit form, save posts through the existing `PlatformSettingsController`.

### Data model

No schema changes — both `AuditLog` and `PlatformSetting` tables already exist.

### Error behavior

No new error codes for the query endpoint (existing `PAGE_SIZE_EXCEEDED` convention applies). No new
codes for settings (already has its own `ApplicationErrors.PlatformSetting.*`).
