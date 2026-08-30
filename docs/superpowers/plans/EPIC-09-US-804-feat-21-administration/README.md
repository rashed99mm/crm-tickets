# FEAT-21 — Administration (audit log + platform settings) · task record

**Spec:** [`../../specs/EPIC-09-US-804-administration.md`](../../specs/EPIC-09-US-804-administration.md)
**Status:** shipped

## SDD gate violation (recorded 2026-08-27)

**No `implementation-plan.md` was ever written or committed for this feature.** Same gap as
FEAT-16 and both FEAT-17 slices: code was written directly from the spec, with only this
retrospective README produced afterward. Not backfilled with a plan dated after the fact — see
[`rubric-traceability.md`](../../../assessment/rubric-traceability.md).

## Evidence

```
dotnet build CustomerSupport.slnx    → Build succeeded, 0 errors
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AuditLogEndpointTests"
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6

dotnet test CustomerSupport.slnx (full suite, all projects)
Passed!  - Failed: 0, Passed: 351, Skipped: 0, Total: 351, Duration: 1m 33s

npx ng build admin-app              → Application bundle generation complete
npx ng test common    --watch=false → Test Files 28 passed | Tests 125 passed
npx ng test admin-app --watch=false → Test Files 17 passed | Tests 121 passed
```

## What shipped

- **A real, load-bearing bug fix, discovered while scoping `US-801`**: `AuditLog`/`IAuditService`
  existed and were fully wired for *writing*, but `AuditBehavior` — the MediatR pipeline component
  whose entire purpose is to call `IAuditService.LogAsync` — was **never registered in the MediatR
  pipeline at all** (only `LoggingBehavior` and `ResponseValidationBehavior` were, in
  `Application/ServiceCollectionExtensions.cs`). Even if it had been registered, it only called
  `ILogger.LogDebug`, never `IAuditService`. The `AuditLogs` table has been permanently empty since
  the platform was adopted. Both gaps fixed: the behavior is registered (deliberately last, so a
  validation failure's short-circuit means it never runs for a request that never reached the
  handler — `AC-145`), and it now calls `IAuditService.LogAsync` with best-effort `Action`/
  `EntityType`/`EntityId` resolved via reflection across the 11 already-designated auditable
  commands. No `OldValues` diff (spec A2) — the behavior has no "before" read to diff against.
- `GET /api/admin/audit-log` (`AdminController`), Admin-gated, filterable by `actionType`/`userId`,
  newest-first (an explicit `SortBy`/`SortDirection` override in the query's constructor — the
  repository's `GetPagedAsync` has no default ordering at all when `SortBy` is unset, which would
  otherwise have made "newest first" accidental rather than guaranteed).
- `AuditLogComponent` — filterable, paginated table with a row-click detail panel.
- `PlatformSettingsComponent` — list + inline per-row edit, consuming the pre-existing
  `PlatformSettingsController` (no backend changes needed for `US-803`, as its own notes said).

## Deviations found during implementation

1. **The `MapFailureStatusCode`-style discovery from `FEAT-16` didn't recur here** — this query's
   only failure path is validation (400), no new `SystemCode`/`SystemCodeMap` entries were needed.
2. **Two of my own integration tests were wrong, not the code**: `AC141`/`AC142` initially assumed
   audit rows already existed from other tests running first (a fragile ordering dependency) and
   that `AuditLog.UserId` was the *entity* acted upon rather than the *actor* who acted. Both fixed
   by building each test's own fixture data and asserting against the admin's own user id.
3. **A hardcoded-string sweep failure of my own making**: `audit-log.component.html` used a raw `·`
   separator between two interpolated values, which `no-hardcoded-strings.spec.ts` correctly
   flagged (that guard's allowlist is deliberately narrow — "every addition is a hole"). Fixed by
   using `—`, already on the allowlist, rather than growing it.

## Not shipped (spec A4, recorded not silently dropped)

- `US-804`/`US-805` (permission entity + admin UI) — a genuinely separate, larger authorization
  capability.
- Date-range filtering on the audit query (spec A5) — the backend query only supports
  `actionType`/`userId`, matching `US-801`'s own AC2/AC3; `US-802`'s AC2 mentions a date range the
  backend doesn't implement.
