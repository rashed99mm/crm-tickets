# FEAT-17 — SLA tracking (second slice: pause/resume + escalation) · task record

**Spec:** [`../../specs/EPIC-05-US-218-sla-escalation.md`](../../specs/EPIC-05-US-218-sla-escalation.md)
**Status:** shipped

## SDD gate violation (recorded 2026-08-27)

**No `implementation-plan.md` was ever written or committed for this feature.** Same gap as the
first FEAT-17 slice and FEAT-16: code was written directly from the spec, with only this
retrospective README produced afterward. Not backfilled with a plan dated after the fact — see
[`rubric-traceability.md`](../../../assessment/rubric-traceability.md).

## Evidence

```
dotnet build CustomerSupport.slnx    → Build succeeded, 0 errors
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SlaPauseAndEscalationEndpointTests"
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6

dotnet test CustomerSupport.slnx (full suite, all projects)
Passed!  - Failed: 0, Passed: 341, Skipped: 0, Total: 341, Duration: 1m 28s
```

## What shipped

- `Ticket.PausedAt`/`TotalPausedSeconds`: entering `Pending` starts the pause, leaving it
  accumulates elapsed time and shifts both due dates forward by the same span (`ChangeStatus`
  carries the logic inline via a new private `ApplySlaPauseTransition`). Fixes a real correctness
  gap in the first SLA slice, which only approximated "paused" by excluding `Pending` from the
  breach scan — a ticket that spent three days in `Pending` would have been immediately flagged
  breached the moment it returned to `Open`, which is exactly what `BR-16`/`BR-17` say must not
  happen.
- `Ticket.EscalationState` (`None`/`Warning`/`Level1`/`Level2`/`Level3` per `BR-32`, only `None` and
  `Level1` reachable this slice). `SlaBreachScanner` calls `ticket.Escalate("Level1")` in the same
  pass that records a ticket's first breach event, so one `SaveChangesAsync` persists both.
- `TicketDetailDto.EscalationState` exposed through the existing ticket-detail read.

## Deviation from the plan, caught before it reached a migration

**The migration EF generated for `EscalationState` defaulted existing rows to `""` (empty string),
not `"None"`.** `TicketConfiguration` had no `.HasDefaultValue(...)` for the new column, so EF fell
back to the CLR default for a non-nullable `string` column being added to a table with existing
rows. That would have left the "always one of five named states" invariant broken for every ticket
that existed before this migration ran. Caught by re-reading the generated migration (the same
review discipline `FEAT-16`'s task record established) before it was ever applied. Fixed: added
`.HasMaxLength(16).HasDefaultValue("None")` to `TicketConfiguration`, then hand-corrected the
already-generated migration, its Designer file and the model snapshot to match (`dotnet ef
migrations remove` needs a live DB connection to check applied-migration state, which this
sandbox doesn't have configured, and the migration was never applied — so this was the safe,
targeted fix rather than requiring a full regenerate).

## Not shipped (spec A2/A3, recorded not silently dropped)

- No `EscalationLevels` config table, no level-2/3 progression, no role-targeted notification —
  per your "without notification for now."
- `US-220` (auto-assignment) — a genuinely separate capability, not touched.
- `US-221` (supervisor override) — re-examined against its own three test cases and found **already
  shipped** by `FEAT-07`'s `AssignTicketCommand` (Supervisor-only, `AC-43`) and `TicketHistory`
  logging every assignment (`AC-48`). Not rebuilt; recorded here so it isn't counted as still open.
- `US-214` (full SLA policy CRUD) — still create + list only.
- Frontend (`US-222`–`US-224`).

## Gaps

- `TotalPausedSeconds` truncates sub-second spans to zero (integer seconds, `(int)elapsed.TotalSeconds`)
  — negligible for any real pause, but worth knowing if a future test exercises very short delays.
- No `SLAEvent` audit row for pause/resume transitions (spec A1) — only the `Ticket`-level fields
  change; there is no historical record of *when* a ticket was paused, only the cumulative total.

## Frontend addendum (2026-08-27)

The agent implementing Tasks 1–5 was killed by the user partway through its final verification
step (after all five tasks' own inline tests had already run green, before the plan's separate
end-of-plan "Full frontend gate" re-run completed) — so this section, and the test evidence in it,
was written afterward directly in this session rather than by that agent, using its staged
(uncommitted) file changes as the record of what it actually did.

### Evidence

```
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC158_GetTickets_ExposesEscalationState|FullyQualifiedName~AC32_GetTickets_ReturnsPagedNewestFirst"
Passed!  - Failed: 0, Passed: 2, Skipped: 0, Total: 2

cd frontend && npx ng test common --watch=false --include='**/sla-countdown.component.spec.ts'
Test Files  1 passed (1)
     Tests  4 passed (4)

cd frontend && npx ng test admin-app --watch=false \
  --include='**/ticket-detail.component.spec.ts' \
  --include='**/ticket-queue.component.spec.ts' \
  --include='**/sla-policies.component.spec.ts'
Test Files  3 passed (3)
     Tests  22 passed (22)

cd frontend && npx ng test common --watch=false --include='**/no-hardcoded-strings.spec.ts' --include='**/rtl-safety.spec.ts'
Test Files  2 passed (2)
     Tests  2 passed (2)
```

`npx ng build admin-app` was confirmed clean separately by serving it (`http://localhost:4200`, 0
errors) — the ticket-detail, ticket-queue and sla-policies chunks all built. The full
`npx ng test admin-app --watch=false` (every spec in the project, not just these three files) has
**not** been re-run since — matching the same external `AiPanelComponent`-missing-import blocker
recorded in the Reporting feature's frontend addendum (a concurrent `FEAT-21` session's own
in-progress file, not touched by this work).

Nothing here is committed — staged only, per explicit instruction this session.

### What shipped

- `TicketListItemDto`/`GetTicketsQueryHandler` project `EscalationState` onto the ticket queue row
  (AC-158's backend prerequisite) — additive, no migration.
- `SlaCountdown` (`common`) — a live countdown component reused for both `responseDueAt` and
  `resolutionDueAt` on the ticket detail screen, colour-coded per addendum A6 (danger once overdue,
  warning under 20% of the created→due window remaining) — AC-155, AC-156.
- Escalation badge and a client-side "sort by escalation" toggle on the ticket queue (A7: sorts the
  currently loaded page only, not a second server-side sort dimension) — AC-158, AC-159.
- An edit form on `SLAPoliciesComponent`, closing the gap its first-slice task record explicitly
  left open (`PUT /api/SLAPolicies/{id}` existed and was unused until now) — AC-157.
- New `--color-warning` design token (`theme.css`) and the dictionary entries the above needed.

### Deviations found during implementation

None recorded by the killed agent's own output before it stopped, and none surfaced re-running its
tests in this session — the staged diff matches the plan's code closely enough that no compile-time
correction was needed (unlike the sibling Reporting addendum, which hit three).

### Gaps

- Full `npx ng test admin-app --watch=false` (all specs) not re-run clean, for the same external
  reason as the Reporting addendum — re-run once `FEAT-21`'s `AiPanelComponent` import is fixed.
- This addendum's evidence was assembled by re-running each task's own test file individually
  rather than observed live task-by-task as the agent worked — equivalent evidence, different
  provenance, recorded so the difference isn't hidden.
