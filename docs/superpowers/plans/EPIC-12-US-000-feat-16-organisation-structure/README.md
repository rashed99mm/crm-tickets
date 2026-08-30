# FEAT-16 — Organisation structure · task record

**Spec:** [`../../specs/EPIC-13-US-311-organisation-structure.md`](../../specs/EPIC-13-US-311-organisation-structure.md)
**Status:** shipped — backend + Department admin screen

## SDD gate violation (recorded 2026-08-27)

**No `implementation-plan.md` was ever written or committed for this feature.** CLAUDE.md's SDD
gate requires a code-bearing plan (`superpowers:writing-plans`) between an approved spec and any
implementation code; this feature went straight from spec to code during a "move fast, ship
epics end to end" stretch, and only this retrospective README was produced afterward. Discovered
2026-08-27 by inspecting the plans folder directly, prompted by the user noticing the folder
looked emptied out. Not backfilled with a plan dated after the fact — CLAUDE.md itself calls that
out as "a transcript, not a spec." Recorded here and in
[`rubric-traceability.md`](../../../assessment/rubric-traceability.md) instead.

## Evidence

```
dotnet build CustomerSupport.slnx    → Build succeeded, 0 errors
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~OrganisationStructureEndpointTests|FullyQualifiedName~DepartmentTests|FullyQualifiedName~BranchTests"
Passed!  - Failed: 0, Passed: 23, Skipped: 0, Total: 23

npx ng build admin-app              → Application bundle generation complete
npx ng test common    --watch=false → Test Files 28 passed | Tests 125 passed
npx ng test admin-app --watch=false → Test Files 17 passed | Tests 121 passed
```

Full-suite `dotnet test CustomerSupport.slnx` (all projects) was **not** re-run after this feature —
only the filtered run above and the existing AC-49 regression check. Should be run before this is
claimed `done` at the traceability-table level, not just `shipped` here.

## What shipped

- `Department`, `Branch` entities (`BaseEntity`, not `AggregateRoot` — no domain events needed),
  explicit `IsActive`/`Deactivate()` matching `Category`'s established lookup-entity pattern (a
  deviation from the spec's A4, which had proposed reusing the generic soft-delete flag — `Category`
  already established the real convention and this follows it instead).
- Nullable `DepartmentId`/`BranchId` on `Ticket`, `Category`, `Customer`, `ApplicationUser` per
  US-303/US-304's table lists exactly. Migration reviewed — touches only the intended tables/columns.
- `DepartmentBranchSeeder`, idempotent like `CategorySeeder`, seeding the well-known-id default
  department/branch (AC-118).
- Full CRUD (`DepartmentsController`, `BranchesController`), Admin-gated mutations, `Authenticated`-gated
  reads (AC-119, AC-120, AC-123).
- **Department admin screen** (`DepartmentsComponent`), wired into `app.routes.ts` and the shell nav
  — US-309, shipped despite the spec's A2 cutting it for time; scope was revised mid-implementation
  when told to ship the epic end to end rather than backend-only.
- **Not shipped: a Branches admin screen.** Never specced (US-309 only covers Department); would be
  the same shape as the Department screen if wanted.
- **Not shipped: `US-306`** (branch-scoped query filters) — blocked on `OQ-5`, unresolved at the
  product level. See spec A1.

## Deviation found and fixed during implementation

**Two new domain error keys (`DEPARTMENT_NOT_FOUND`, `BRANCH_NOT_FOUND`) were never registered in
`SystemCodeMap`/`SystemCode`, and this codebase's `MapFailureStatusCode` derives the HTTP status
entirely from the resolved `SystemCode` — not from `MessageType` on the `Response`.** An unmapped
domain key silently resolved to `ERR005` (the internal-error fallback), which `MapFailureStatusCode`
has no case for, so it fell through to 400 instead of 404. Caught by
`AC119_UnknownDepartmentId_Returns404` and two siblings failing. Fixed by adding `ERR047`/`ERR048`
and their `SystemCodeMap` entries, and their cases in `ResponseExtensions.MapFailureStatusCode`.

**A second, related gap:** the new `UX_Departments_Name`/`UX_Branches_Name` unique indexes had no
paired `IDbExceptionTranslator` handling in the Create/Update handlers — this codebase always pairs
a unique index with 409 handling (see `CreateCustomerCommandHandler`), and the index was added
without it. A duplicate name would have 500'd instead of 409'ing. Fixed: both entities'
Create/Update handlers now catch `IsUniqueViolation` and return `NAME_EXISTS` (409), with matching
`ERR049`/`ERR050` codes, `SystemCodeMap` entries, and bilingual resource pairs. Proven by
`AC121_CreateDepartment_DuplicateName_Returns409`.

**This class of bug (new domain key registered in `ApplicationErrors`/`Resources.yaml` but not in
`SystemCode`/`SystemCodeMap`/`MapFailureStatusCode`) is worth a project-wide sweep** — any other
feature that introduced a *new* failure code (not reusing an existing one) is at risk of the same
silent 400 fallback. `FEAT-14` (conversation record) was not at risk because it reused
`Ticket.NOT_FOUND`, already mapped.

## Gaps

- No full-suite backend test run since this feature landed (only the filtered run above).
- No component test for `DepartmentsComponent` (list/create/deactivate) — implemented and manually
  build-verified, not proven per-AC the way this project's convention asks for. Same category of gap
  as `FEAT-14`'s frontend.
- Branches admin screen not built (not asked for).
