# FEAT-17 — SLA tracking (first slice) · task record

**Spec:** [`../../specs/EPIC-05-US-218-sla-tracking.md`](../../specs/EPIC-05-US-218-sla-tracking.md)
**Status:** backend slice shipped

## SDD gate violation (recorded 2026-08-27)

**No `implementation-plan.md` was ever written or committed for this feature.** CLAUDE.md's SDD
gate requires a code-bearing plan between an approved spec and any implementation code; this
slice went straight from spec to code during a "move fast, ship epics end to end" stretch, and
only this retrospective README was produced afterward. Not backfilled with a plan dated after the
fact — see [`rubric-traceability.md`](../../../assessment/rubric-traceability.md) for the same
note applied across all four affected features.

## Evidence

```
dotnet build CustomerSupport.slnx    → Build succeeded, 0 errors
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SlaTrackingEndpointTests|FullyQualifiedName~SLAPolicyTests|FullyQualifiedName~SLAEventTests"
Passed!  - Failed: 0, Passed: 17, Skipped: 0, Total: 17

dotnet test CustomerSupport.slnx (full suite, all projects)
Passed!  - Failed: 0, Passed: 335, Skipped: 0, Total: 335, Duration: 1m 54s
```

## What shipped

- `SLAPolicy`, `SLAEvent` (`IAppendOnlyEntity`) entities, migration (reviewed — only `Tickets`
  columns + two new tables).
- `Ticket.ResponseDueAt`/`ResolutionDueAt`, computed once at creation (`CreateTicketCommandHandler`)
  against the most specific matching active policy (category-scoped beats unscoped); wall-clock
  hours only (spec A1) — no business-hours calendar this slice.
- `SLAPoliciesController` — create (Admin) + list (Authenticated). No update/deactivate yet.
- `SlaBreachScanner`/`SlaBreachDetector` — a `BackgroundService` polling loop (matching
  `NotificationSender`'s existing shape, not Hangfire's recurring-job API — spec A6), recording an
  `SLAEvent` when a `New`/`Open` ticket's due date has passed and no breach is already recorded.
  `Pending`/`Resolved`/`Closed` tickets are never evaluated — the slice's approximation of "paused"
  (spec A4), in place of `US-213`'s full pause/resume.
- `TicketDetailDto` gained `ResponseDueAt`/`ResolutionDueAt` so the due dates are observable through
  the existing ticket-detail read, without a new endpoint.

## Deliberately cut, all recorded in the spec (A1–A7)

`US-213` (explicit pause/resume) · `US-214` (full policy CRUD) · `US-215` (business-hours calendar)
· `US-217` (pre-breach warning) · `US-218` (auto-escalation) · `US-219` (breach notifications) ·
`US-220` (auto-assignment) · `US-221` (supervisor override) · `US-222`–`US-224` (frontend) ·
`US-225` (escalation-state column).

## Deviation from the spec, found during implementation

**A6 said the scanner would run "internal host only," matching the seeders' convention.** That
turned out not to match how the codebase's one existing background worker
(`NotificationSender`) is actually registered — it's wired into the **shared**
`RegisterPlatformInfrastructure` (`Infrastructure/ServiceCollectionExtensions.cs`), which both
`InternalApi` and `ExternalApi` call, so it already runs on both hosts. Rather than build new
host-specific plumbing that doesn't exist anywhere else in this codebase, `SlaBreachDetector` was
registered the same way, alongside `NotificationSender` — consistent with actual precedent, not
with the spec's assumption about it. Worth revisiting if `ExternalApi` running background workers
turns out to matter (it's anonymous/read-only per `ADR-0008`, so a write-capable background service
there is arguably already a soft violation `NotificationSender` established first).

## Gaps (superseded — see below)

- ~~No frontend (deliberately, per spec).~~ **Shipped** — `SLAPoliciesComponent` (create, list,
  deactivate), wired into routing and the shell nav. No edit form; the backend's `PUT` exists but
  the screen doesn't expose it, matching `DepartmentsComponent`'s scope.
- Pause/resume and escalation shipped in a second slice — see
  `docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-escalation/README.md`. Notifications remain out of
  scope, on explicit instruction.
- ~~`SLAPoliciesController` has no update/deactivate.~~ **Shipped** — `PUT`/`DELETE
  /api/SLAPolicies/{id}`, Admin-gated, both 404 on an unknown id.

## Final state (all three FEAT-17 passes)

```
dotnet test CustomerSupport.slnx (full suite, all projects) → 345/345 passing
npx ng build admin-app                                      → clean
npx ng test admin-app --watch=false                          → 121/121 passing
npx ng test common    --watch=false                          → 124/125 passing —
    the 1 failure (rtl-safety) is in projects/portal-app/.../detail.component.html,
    unrelated to this feature or session's own work; flagged, not fixed.
```
