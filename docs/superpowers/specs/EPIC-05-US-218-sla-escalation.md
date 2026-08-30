# SLA tracking — second slice: pause/resume and auto-escalation

**Sprint:** 8 (second slice) · **Feature:** `FEAT-17` · **Stories:** `US-213`, `US-225`, `US-218` (partial) ·
**Epic:** `EPIC-05` SLA & Escalation

## Problem

The first SLA slice (`EPIC-05-US-218-sla-tracking.md`) computes due dates and detects breaches,
but approximates "paused" by simply excluding `Pending` tickets from the scan (spec A4 of that
design). That means a ticket sitting in `Pending` for three days, then returned to `Open`, is
immediately flagged breached — the clock kept running while the agent was waiting on the customer,
which is exactly what `BR-16`/`BR-17` say must not happen. Separately, a detected breach today is
recorded (`SLAEvent`) but nothing about the ticket itself changes — no one browsing the queue can
see that a ticket is in trouble.

## Assumptions

A1. **No `SLAEvent` audit rows for pause/resume**, despite `US-213`'s notes suggesting it. `SLAEvent`
    exists to record breach tracking against a `TargetType` of `Response`/`Resolution`; a pause/resume
    transition is not a breach event and forcing it into that shape would be misusing the type to
    avoid defining a new one. The observable effect — the due dates shifting — is what `BR-16`/`BR-17`
    actually ask for, and that lives on the `Ticket` itself.

A2. **No configurable `EscalationLevels` table, no level progression, no role-targeted notification.**
    `US-218`'s full design (a table of levels, each with a target role and a breach-minutes
    threshold, notifying that role) is real scope — and the user asked explicitly to skip
    notifications this pass. What ships: the **first** breach recorded for a ticket sets
    `EscalationState` to `"Level1"`. There is no `"Level2"`/`"Level3"` progression logic — nothing
    in this slice re-evaluates an already-escalated ticket, so those two enum values exist (per
    `BR-32`) but nothing ever sets them. `"Warning"` (pre-breach) is similarly unused — it belongs to
    `US-217`, cut in the first slice and still cut here.

A3. **`US-220` (auto-assignment) and `US-221` (supervisor override) are not this slice.**
    Auto-assignment is a genuinely separate capability (round-robin/load-based strategy config, new
    tables) unrelated to the breach/escalation loop this slice closes. `US-221`'s actual asks —
    supervisor-only reassignment, logged in the ticket's audit trail — are **already shipped**:
    `AssignTicketCommand` (`FEAT-07`) is `Supervisor`-only (`AC-43`) and every assignment is appended
    to `TicketHistory` (`AC-48`). Re-examined against `US-221`'s three test cases: `TC-01`
    (`AssignTicketCommandHandler` sets `AssigneeId`), `TC-02` (`TicketHistory.Record` on every
    assign/reassign), `TC-03` (`AC43_Agent_AssigningAnyTicket_Returns403`) are all already proven by
    existing tests. `US-221` is effectively closed by prior work, not by this slice — recorded here
    so it is not silently forgotten as "not started."

A4. **`US-214` (full SLA policy CRUD) remains cut.** Not touched this slice either.

A5. **2026-08-27: the frontend cut above (`US-222`–`US-224`) is reopened by the addendum at the
    end of this document.** Everything above this point (problem, A1–A4, out of scope, AC-134
    through AC-139, design) describes the original backend-only slice exactly as approved and
    implemented, and is left untouched. The addendum is new content appended after it, not a
    revision of it.

## Out of scope

- `US-217` — pre-breach warning (`"Warning"` state, notifications).
- `US-219` — breach notifications.
- `US-220` — auto-assignment.
- Level 2/3 escalation progression and the `EscalationLevels` config table (A2).
- `SLAEvent` audit rows for pause/resume (A1).

`US-222`–`US-224` (frontend) were cut here originally and are **added back by the 2026-08-27
addendum below** — see A5.

## Acceptance criteria

AC-134. Given a ticket with `ResponseDueAt`/`ResolutionDueAt` set, when its status transitions to
`Pending`, then `PausedAt` is set to the transition time and `TotalPausedSeconds` is unchanged.

AC-135. Given a ticket in `Pending` with `PausedAt` set, when its status transitions away from
`Pending`, then `TotalPausedSeconds` increases by the elapsed pause duration, `PausedAt` is cleared,
and both due dates (where set) shift forward by that same duration.

AC-136. Given a ticket cycles through `Pending` more than once, then `TotalPausedSeconds` accumulates
across every cycle rather than reflecting only the most recent one.

AC-137. Given a new ticket, its `EscalationState` is `"None"`.

AC-138. Given the breach-detection scan records a new breach `SLAEvent` for a ticket whose
`EscalationState` is `"None"`, then that ticket's `EscalationState` becomes `"Level1"` in the same
pass.

AC-139. Given a ticket already at `EscalationState` `"Level1"`, when a second, different-target-type
breach is recorded for it (e.g. it already breached `Response` and now breaches `Resolution` too),
then `EscalationState` remains `"Level1"` — it is not reset or duplicated (A2: no progression this
slice).

## Design

### Backend: Domain

**Edit:** `Ticket` gains `PausedAt` (`DateTime?`), `TotalPausedSeconds` (`int`, default 0),
`EscalationState` (`string`, default `"None"`). `ChangeStatus` gains the pause/resume logic inline
(AC-134/AC-135): entering `Pending` sets `PausedAt` if not already set; leaving `Pending` computes
the elapsed span, accumulates it, clears `PausedAt`, and shifts both due dates by that span. A new
`Escalate(string level)` method, called only from the breach scanner, sets `EscalationState`
unconditionally to the given value — the "only from `None`" rule (AC-138) is the *caller's*
responsibility (the scanner checks before calling), not a guard inside the method, because the
method has no way to know AC-139's "already escalated, leave it" is even a rule versus a future
slice legitimately wanting to force a level.

### Backend: Infrastructure

**Edit:** `SlaBreachScanner.ScanAsync` — after appending a new `SLAEvent` for a ticket (either
target type), if that ticket's `EscalationState` is still `"None"`, call `ticket.Escalate("Level1")`
in the same iteration, so one `SaveChangesAsync` call persists both the event and the state change
together.

### Backend: Application

**Edit:** `TicketDetailDto` gains `EscalationState` (string) — observable through the existing
ticket-detail read, matching how `ResponseDueAt`/`ResolutionDueAt` were exposed in the first slice.

### Data model

One migration: `PausedAt`, `TotalPausedSeconds`, `EscalationState` columns added to `Tickets`. No
new tables.

### Error behavior

No new error codes — nothing in this slice introduces a new failure path; `ChangeStatus`'s existing
transition-table refusal (`AC-38`) is unaffected by the pause/resume logic riding inside it.

---

## Addendum (2026-08-27): frontend — SLA countdown, policy edit, escalation badge

**Stories:** `US-222`, `US-223` (edit only — list/create/deactivate already shipped), `US-224`.
Written because both slices of this feature shipped without ever producing a saved
`implementation-plan.md` (recorded in this feature's task-record README and in
`docs/assessment/rubric-traceability.md`); this addendum and its plan close that gap correctly —
spec and plan both precede the code they describe.

### Problem

The ticket detail screen and queue already carry `ResponseDueAt`/`ResolutionDueAt`/
`EscalationState` in their API responses (`TicketDetailDto`, shipped `FEAT-17`), but nothing in the
UI shows them: an agent has to guess how much time is left, and a supervisor scanning the queue has
no visual signal that a ticket has already escalated. Separately, `SLAPoliciesComponent` (shipped
first slice) has no edit path even though the backend's `PUT /api/SLAPolicies/{id}` has existed
since that same slice.

### Assumptions

A6. **The warning threshold for the countdown is derived, not configured.** No policy field or API
    response carries an explicit "warning at X% remaining" value. The countdown treats the window
    from `Ticket.CreatedAt` to the due date as 100%, and switches to warning style once less than
    20% of that window remains (and to danger style once the due date has passed). Chosen because
    it needs no new backend field and degrades sensibly for both short and long SLA windows; a
    fixed-minutes threshold would not.

A7. **"Sort by escalation" (`US-224` AC2) sorts only the currently loaded queue page, not the full
    result set across pages.** The queue's `GetTicketsQuery` has no dynamic `SortBy` wiring today —
    it always orders by `CreatedAt` descending server-side — and adding a second server-side sort
    dimension is a larger change than this addendum's scope. The sort is a client-side re-order of
    the rows already on screen. Recorded as a real limitation, not silently full-dataset behaviour.

A8. **`TicketListItemDto` gains `EscalationState`.** It's already computed and stored on `Ticket`
    (`FEAT-17` second slice) and already exposed on `TicketDetailDto` — this is a projection-field
    addition, not new business logic, needed so the queue badge (AC-158) has something to render
    without a second request per row.

### Out of scope

- Server-side sort-by-escalation across the full result set (A7).
- A configurable warning-threshold percentage (A6) — fixed at 20% this pass.
- Any change to `SLAPoliciesComponent`'s list/create/deactivate behaviour — only the edit gap
  closes.

### Acceptance criteria

AC-155. Given a ticket detail view for a ticket with `ResponseDueAt` and/or `ResolutionDueAt` set,
when the view renders, then each set due date is shown as a countdown of time remaining that updates
without a page reload (`US-222` AC1).

AC-156. Given the SLA countdown is displayed for a due date that has not passed, when the remaining
time is less than 20% of the window between `CreatedAt` and that due date (A6), then the countdown
renders in the warning style; when the due date has passed, then it renders in the danger style;
otherwise it renders in the normal style.

AC-157. Given the SLA policies screen, when an admin selects an existing policy's edit action and
submits valid changed values, then `PUT /api/SLAPolicies/{id}` is called with those values and the
policy row in the list reflects the update on success.

AC-158. Given the ticket queue, when a row's ticket has `EscalationState` other than `"None"`, then
an escalation badge naming the level is rendered on that row.

AC-159. Given the ticket queue with a mix of escalated and non-escalated rows, when the user
toggles "sort by escalation," then rows with `EscalationState` other than `"None"` are reordered
to the top of the currently loaded page (A7).

### Design

**Backend (small, additive — no new endpoint):**
`TicketListItemDto` (`CustomerSupport.Application/Features/Tickets/Dtos/TicketDtos.cs`) gains
`string EscalationState` as its last positional parameter. `GetTicketsQueryHandler`'s
`ListProjectedOrderedAsync` projection and the final `TicketListItemDto` construction both add
`t.EscalationState` (A8). No migration, no new query parameter, no new error path.

**Frontend — new/edited files (admin-app + common):**
- `common`: `SlaCountdown` — a small standalone component/pipe computing the AC-156 percentage and
  style class from `createdAt` + a due date, reused by the ticket detail screen for both due dates.
- Ticket detail component: renders two `SlaCountdown` instances (response, resolution) when their
  due date is non-null.
- Ticket queue component: adds an escalation-badge cell reading `EscalationState`, and a "sort by
  escalation" toggle that re-sorts the currently loaded `signal` array client-side (A7).
- `SLAPoliciesComponent`: adds an edit action per row, reusing the existing create form in edit mode
  (pre-populated, calling `PUT` instead of `POST` on submit) rather than a second form component.

### Error behavior

No new error codes. The policy edit path reuses the existing `PUT /api/SLAPolicies/{id}` error
surface (404 unknown id, 400 validation) already proven by the backend's own tests.

---

## Addendum (2026-08-28): backend — multi-level auto-escalation progression (`US-218`)

**Story:** `US-218` · **Original AC:** `AC-5.7` → `AC-218.1`/`AC-218.2`/`AC-218.3`.

### Override of A2 (recorded per SDD gate)

This addendum **supersedes assumption A2 and AC-139** for the `US-218` backend work. A2 said the
second slice ships only the first breach setting `EscalationState` to `"Level1"` with no further
progression, and AC-139 asserted a later breach "remains `Level1`". The user, when choosing between
the two competing US-218 plans, selected the **auto-escalation progression** design
(`docs/superpowers/plans/EPIC-05-US-218-auto-escalation/tasks/01-auto-escalation.md`) over the
`implementation-plan.md` variant. That is a deliberate override, with the same precedent as the
business-hours calendar override of the wall-clock default. This addendum records the real
multi-level behaviour so the spec precedes the code (SDD gate step 2).

### Assumptions

A9. **Level definitions are seeded, not admin-configured this pass.** The new `EscalationLevels`
    table exists with `Level` uniqueness and positive `BreachMinutes`, but **no endpoint is added**
    (task: "No endpoint is added"). Default `Level1`/`Level2` rows come from an idempotent seeder
    (`EscalationLevelSeeder`), mirroring `CategorySeeder`/`DepartmentBranchSeeder`. The task's
    authorization note applies only to a future admin endpoint, not to this pass.

A10. **System actor identity is server configuration.** The `Escalated` history row needs a
     non-empty `ActorId` (`TicketHistory.Record` refuses `Guid.Empty`). A well-known system GUID
     constant (`SystemActors.EscalationEngine`) is used, matching the
     `DepartmentBranchSeeder.DefaultBranchId` well-known-id pattern.

A11. **`EscalationState` doubles as the transition cursor.** The ticket records its current level
     as a string (`"None"`/`"Level1"`/`"Level2"`). The next level is the lowest active `EscalationLevel`
     whose `Level` is numerically greater than the current one; **terminal** is "no higher active
     level exists", not a magic `Level3` branch (task: "terminal-by-absence-of-higher-level").

A12. **`ISlaBreachScanner.ScanAsync` keeps returning the breach-event count.** The escalation work
     is additive; existing breach-count callers (`SlaBreachDetector`, AC-138 test) are preserved.

A13. **`TicketHistory` gains an `Escalated` change type.** `TicketChangeType` grows a sixth value so
     the escalation transition is auditable like every other lifecycle change (AC-48/AC-49 append-only).

### Out of scope

- A configurable escalation admin endpoint (seeded only, A9).
- Role-targeted notification delivery as a *consumer* — the `SlaEscalatedMessage` is published with
  its `TargetRole`, but no notification consumer is wired this pass (A2's notification cut stays).

### Acceptance criteria

AC-218.1. Given an active escalation governed by a seeded set of levels, when the breach scanner
records a first breach for a ticket whose `EscalationState` is `"None"`, then the ticket advances to
the lowest configured level (`"Level1"`) **and** one `Escalated` history row is appended recording
the previous and next levels under the system actor (unit: `AC2181_*`; integration:
`AC2181_BreachScanner_SetsLevel1AndAppendsHistory`).

AC-218.2. Given a ticket already escalated to a non-terminal level, when a later qualifying breach
is recorded, then the ticket advances to the next higher active level — stopping at the highest
configured level, with no further history once terminal (unit: `AC2182_*`; integration:
`AC2182_SecondQualifyingBreach_SetsLevel2AndPublishesRoleTarget`, `AC2182_TerminalLevel_DoesNotCreateFurtherHistory`).

AC-218.3. Given repeated exposure to the same breach condition, then no duplicate transition is
applied and a single `Escalated` history row results per transition; concurrent scanner passes claim
the transition exactly once and a stale row version is retried or treated as already-applied, never
surfaced as a 500 (unit: `AC2183_*`; integration: `AC2183_ConcurrentScannerRuns_CreateOneTransition`,
`AC2183_PendingOrResolvedTicket_DoesNotEscalate`).

### Design

**Domain.** New `EscalationLevel : BaseEntity` (`Level` `string`, `BreachMinutes` `int` positive,
`TargetRole` `string?`, `IsActive` `bool`) with a `Create` factory. `TicketChangeType` gains
`Escalated`. `Ticket` gains `AdvanceEscalation(string previous, string next, Guid systemActor)` — a
guarded transition that rejects the unknown/empty cursor, rejects a downward or non-greater move, and
appends exactly one `Escalated` history row; `Escalate(level)` (used by the pre-progression AC-138
path) is kept for backward compatibility.

**Shared.Contracts.** New `SlaEscalatedMessage` carrying `TicketId`, `Reference`, `PreviousLevel`,
`NextLevel`, `TargetRole`, `BreachMinutes`, `BreachedAt`; its topic is added to `Topics` as
`sla.messages.escalated`.

**Infrastructure.** `EscalationLevelConfiguration` (table `EscalationLevels`, unique index on
`Level`, positivity + active defaults). `AppDbContext` gains `DbSet<EscalationLevel> EscalationLevels`.
`EscalationLevelSeeder` seeds `Level1`(60)/`Level2`(240) idempotently. `SlaBreachScanner` refactored
to: record the breach event, select the next active level above the current `EscalationState`, apply
`ticket.AdvanceEscalation(...)`, persist everything in one `SaveChangesAsync`, then publish **one**
`SlaEscalatedMessage`. Concurrency handled with a duplicate-key no-op on the unique `Level` index /
row-version retry on the ticket.

**Application.** A port (method-based, not EF queryables) for level lookup and next-level selection
lives under `CustomerSupport.Application/Interfaces/` so the scanner (Infrastructure) does not query
escrow state directly — kept behind the interface boundary per the dependency rule.

### Data model

One migration `AddEscalationLevels`: creates the `EscalationLevels` table. No columns change on
`Tickets` (the existing `EscalationState` column already holds `Level1`/`Level2`).

### Error behavior

No new error codes and no new endpoint surface. A stale row version during concurrent scans is
retried or treated as an already-applied transition (AC-218.3), never a 500. The future admin
endpoint, when built, must use `Admin` auth, `400` field validation, `403` for non-Admin, `404`
missing level, and generic `500 ProblemDetails` (task authorization note).
