# Reporting (FEAT-19+) · task record

**Plan:** [`implementation-plan.md`](./implementation-plan.md)
**Spec:** [`../../specs/EPIC-08-US-606-reporting.md`](../../specs/EPIC-08-US-606-reporting.md)
**Status:** shipped

## Evidence

```
dotnet build CustomerSupport.slnx    → Build succeeded, 0 errors
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportsEndpointTests"
Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8

dotnet test CustomerSupport.slnx (full suite, all projects)
Passed! (363) / Failed: 1 — the 1 failure is `AiAssistEndpointTests.AC212_*`, a DIFFERENT,
concurrently-in-progress feature's own TDD red state (its /api/Tickets/{id}/ai/summary endpoint
does not exist yet) — not a regression from this task. Total 364.
```

## What shipped

- `ReportsController` — `GET /api/reports/ticket-volume`, `sla-performance`, `agent-performance`,
  all gated by the `Supervisor` policy (already Supervisor-or-Admin per `AuthorizationExtensions.cs`).
- Ticket volume: three independent breakdowns (period/category/priority) over a date range.
- SLA performance: met/breached counts by priority, reading `SLAEvent` as the single source of
  truth for "breached" (spec A6) rather than re-deriving it from raw timestamps.
- Agent performance: tickets resolved and an approximate average handle time per agent (spec A7 —
  `UpdatedAt - CreatedAt`, not a precise first-resolution timestamp).

## Deviation: `US-608` (report scoping) adapted, not built as specced

The story assumes a `Manager` role and a `departmentId` JWT claim, neither of which exists in this
codebase (only `Admin`/`Supervisor`/`Agent` plus four inherited-platform roles). Worse, department
*filtering* would filter on a column — `Ticket.DepartmentId`/`ApplicationUser.DepartmentId`
(`FEAT-16`) — that nothing has ever assigned, so it would always be `NULL` for every row. Built
instead: role-gated to `Admin`/`Supervisor` at the controller, no department filter. Recorded, not
silently substituted.

## A concurrent-editing collision, handled and recorded

While implementing, `dotnet build` failed on `AiAssistEndpointTests.cs` — a file from a **different,
concurrently-running session** building `FEAT-21` (AI assist, sprint 15), not part of this task.
Its test referenced an undefined local `TicketDetail` type and an undefined `SystemCode.ERR052`
constant — both expected TDD red-state gaps for unfinished work, not bugs. Fixed the absolute
minimum to unblock compilation for everyone sharing the repo: added the `ERR052` constant (a data
value, safe to add regardless of who finishes wiring it up) and completed one line to use the real
`TicketDetailDto` instead of a stub, matching a change the other session made to the same file
*while this task was in progress* — confirmed by a second live diff notification mid-edit. Did not
implement any part of the actual AI-assist feature; that endpoint still correctly 404s, which is why
`AC212_Summary_WithoutProvider_ReturnsNotConfigured` fails until that other session finishes its own
work.

## Not shipped (spec A2–A4, recorded not silently dropped)

- `US-605` (CSAT report) — no rating-collection mechanism exists anywhere in this codebase.
- `US-609` (export) — needs a CSV/Excel package not in `Directory.Packages.props`, and depends on
  `US-610` (frontend), also not built.
- `US-606`, `US-607`, `US-610` (frontend) — backend-only this slice. **Superseded 2026-08-27** by
  the frontend addendum below, which ships an adapted `US-610` alongside the three reports.

## Frontend addendum (2026-08-27)

**Spec:** the addendum at the bottom of
[`../../specs/EPIC-08-US-606-reporting.md`](../../specs/EPIC-08-US-606-reporting.md), `AC-160`–`AC-164`.
**Plan:** the "Addendum (2026-08-27): frontend" section at the bottom of
[`implementation-plan.md`](./implementation-plan.md), Tasks 4–6.
**Status:** implemented and staged, not committed — the user asked that nothing be committed until
they say so; every file below is `git add`-ed but sitting uncommitted for review.

### Evidence

```
cd frontend && npx ng test common --watch=false --include='**/report-date-range-filter.component.spec.ts'
Test Files  1 passed (1)
     Tests  1 passed (1)

cd frontend && npx ng test admin-app --watch=false \
  --include='**/ticket-volume-report.component.spec.ts' \
  --include='**/sla-performance-report.component.spec.ts' \
  --include='**/agent-performance-report.component.spec.ts'
Test Files  3 passed (3)
     Tests  4 passed (4)

cd frontend && npx ng test common --watch=false --include='**/guards.spec.ts'
Test Files  1 passed (1)
     Tests  3 passed (3)

cd frontend && npx ng test common --watch=false   (full common-lib suite, run at the end)
Test Files  32 passed (32)
     Tests  136 passed (136)
```

`npx ng build admin-app` and the full `npx ng test admin-app --watch=false` **could not be run
clean** — both fail with the same error, unrelated to this addendum:

```
NG1010: 'imports' must be an array of components, directives, pipes, or NgModules.
  projects/admin-app/src/app/features/tickets/ticket-detail.component.ts:46:4
    AiPanelComponent,
TS2304: Cannot find name 'AiPanelComponent'.
```

`ticket-detail.component.ts` references `AiPanelComponent` in its `imports` array with no
corresponding import statement — this is `FEAT-21` (AI assist)'s own in-progress work from a
concurrent session, not part of this addendum or this plan, and not a file either of this
session's two parallel frontend forks (this one, and the SLA-escalation frontend fork) was
assigned to touch. Confirmed by isolating it: the full `common` library suite (32/32 files,
136/136 tests, including `no-hardcoded-strings` and `rtl-safety`) is completely green, and every
individual report/guard spec targeted directly with `--include` passes — the failure is specific
to `ticket-detail.component.ts` and appears identically in both a scoped admin-app test run and a
full `ng build admin-app`, regardless of what is included. Not fixed here: adding the missing
import for a component this session did not build carries real risk of guessing its wrong source
module.

### What shipped

- `ReportsApi` (`common`) — typed client for the three existing report endpoints, matching
  `TicketApi`'s established pattern (no envelope handling in the service — that's the interceptor's
  job).
- `ReportDateRangeFilter` (`common`) — shared date-range filter component (`AC-163`), reused by all
  three screens below.
- `TicketVolumeReportComponent`, `SlaPerformanceReportComponent`, `AgentPerformanceReportComponent`
  (admin-app) — one screen per shipped endpoint (`AC-160`, `AC-161`, `AC-162`), each syncing its
  date range to the url's query params for shareability (`AC-163`).
- Three routes under the admin shell (`/reports/ticket-volume`, `/reports/sla-performance`,
  `/reports/agent-performance`) and one "Reports" nav entry, gated by an extended `roleGuard` that
  now accepts multiple roles (`roleGuard('Supervisor', 'Admin')`) — matching the backend's
  `Supervisor` policy exactly (`AC-164`). `roleGuard('Admin')` (every existing call site) is
  unchanged.

### Deviations found during implementation

1. **`translations.ts` was found already broken twice, mid-task, from a concurrent process (not
   this session's SLA-escalation fork) appending dictionary entries *after* the object's closing
   brace** — `'ai.summary'`… and later `'ai.notAvailable'`/`'ai.chat.*'`/`'ai.ungrounded'` — a real
   TypeScript syntax error that would have blocked every consumer of `common`, not just this
   addendum. Both times, repaired by moving the stranded entries back inside the object (content
   preserved verbatim, only repositioned) rather than deleting anything unrecognised.
2. **`{{ ('reports.groupBy.' + option) | t }}` doesn't type-check** — `TranslatePipe` is typed
   against the dictionary's literal key union, and template-side string concatenation isn't a
   literal. Fixed with a small `GROUP_BY_LABEL_KEYS` lookup map in the component instead, exposed
   via a `groupByLabel()` method.
3. **`| number: '1.0-1'` needs `DecimalPipe` imported and added to the component's `imports`
   array** — Angular's built-in pipes aren't globally available to standalone components. Added.

None of these are deviations from the plan's *design* — the plan's code was correct in intent; these
were compile-time details the plan's inline snippets didn't spell out, fixed while implementing.

### Gaps

- Still no dashboard/live-queue/CSAT/branch-filter frontend — unchanged from spec addendum A4;
  `US-606`/`US-607` as originally written remain out of scope.
- `npx ng build admin-app` / full `npx ng test admin-app --watch=false` have not been run clean, for
  the external reason above — re-run both once `ticket-detail.component.ts`'s `AiPanelComponent`
  import is fixed by whoever is building `FEAT-21`, before treating this addendum as fully gated.
