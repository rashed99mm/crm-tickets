# Task 01 — Business-Hours Calendar

**Story/AC:** US-215, original AC-5.4 / spec AC-215.1..2
**Layer:** Backend domain/application/infrastructure plus admin UI
**Status:** not started; previously cut from FEAT-17

## Executable checklist

- [ ] Record approval to reopen the previously cut wall-clock-only assumption. Inspect `Branch`,
  `Ticket`, SLA creation/scanner, current branch authorization, and organisation Angular screens.
- [ ] First add failing domain tests in `Unit/BusinessHoursCalculatorTests.cs`:
  `CalculateDuration_ExcludesWeekends`, `CalculateDuration_ExcludesPublicHolidays`,
  `CalculateDuration_RespectsWorkingHours`, `CalculateDuration_UsesBranchTimezoneAndReturnsUtc`,
  `CalculateDuration_HandlesDstTransition`, and `CalculateDuration_UsesWallClockWhenCalendarMissing`.
- [ ] Add failing validation tests in `Unit/BusinessHoursCalendarValidatorTests.cs`:
  `RejectsOverlappingIntervals`, `RejectsCloseBeforeOpen`, `RejectsInvalidTimezone`, and
  `RejectsDuplicateHoliday`.
- [ ] Add domain calendar/holiday entities and `IBusinessHoursCalculator` implementation; keep UTC
  storage and deterministic timezone/DST behavior.
- [ ] Add DbSets/configurations/indexes/migration and the business-hours CRUD handlers/DTOs.
- [ ] Add failing integration methods `Admin_CreatesAndReadsBranchCalendar`,
  `NonAdmin_CannotModifyBranchCalendar`, `InvalidCalendar_ReturnsFieldErrors`, and
  `TicketCreation_UsesBusinessHoursForDueDate`.
- [ ] Wire `IBusinessHoursCalculator`, controller route, and SLA caller. Preserve wall-clock fallback
  only when no calendar exists.
- [ ] Add Angular API/editor/tests only if the reopened slice includes admin CRUD; use exact test names
  from the parent plan and translated error states.
- [ ] Run targeted tests/builds and paste actual output. Do not mark complete from design or migration
  generation alone.

## Exact files

- New domain: `Entities/Sla/{BusinessHoursCalendar,BusinessHoursInterval,PublicHoliday}.cs` and
  `Services/{IBusinessHoursCalculator,BusinessHoursCalculator}.cs`.
- New application/API: `Features/Sla/BusinessHours/*`, `BusinessHoursController.cs`.
- New persistence/tests: two configuration files, migration, three backend test files.
- Modify: `AppDbContext.cs`, `BranchConfiguration.cs` if timezone is stored on Branch,
  SLA creation/scanner, `ServiceCollectionExtensions.cs`.
- Conditional frontend: `common/.../business-hours.api.ts`, admin component/template/spec and route.

## Verification commands

```powershell
cd backend
dotnet test CustomerSupport.slnx --filter FullyQualifiedName~BusinessHours
dotnet test CustomerSupport.slnx
dotnet build CustomerSupport.slnx
cd ..\frontend
npx ng test admin-app --watch=false --include "**/business-hours.component.spec.ts"
npx ng build admin-app
```

## Status evidence

Record reopen decision, migration name, exact calculator cases, due-date UTC values, authorization
responses, frontend test/build counts, and sanitized command output. No commands have been run while
writing this plan.

## Deviation record

`None yet.` Record overnight interval, timezone/DST, fallback, migration, and UI-scope decisions as
explicit deviations with owners.
