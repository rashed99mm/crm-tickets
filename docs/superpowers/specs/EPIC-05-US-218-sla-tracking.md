# SLA tracking — policies, targets and breach detection

> **Historical first-slice record.** This document records the original `FEAT-17` scope. The
> current lifecycle and SLA behavior are defined by
> [`EPIC-05-US-218-phase2-bi-workflow.md`](./EPIC-05-US-218-phase2-bi-workflow.md) and the
> implementation in `Ticket.cs`, `TicketStatus.cs`, `SlaBreachScanner.cs`, and
> `SlaBreachDetector.cs`.

**Sprint:** 8 (first slice) · **Feature:** `FEAT-17` · **Stories:** `US-210`, `US-211`, `US-212`, `US-216` (historical slice) ·
**Epic:** `EPIC-05` SLA & Escalation

## Problem

Nothing today tracks whether a ticket is answered or resolved within a committed time. This is the
first vertical slice of the SLA epic: define a target per priority, compute due dates when a ticket
is created, and detect when a ticket has gone past its target.

## Assumptions

A1. **Wall-clock hours only — no business-hours calendar.** `US-215` (branch calendars, holidays) is
    its own 5-point story with real complexity (weekend/holiday-aware duration arithmetic). `US-212`'s
    own notes say explicitly: "Without a calendar, targets are computed using wall-clock hours" — that
    fallback *is* what ships this slice, not an invented shortcut. `ResponseDueAt`/`ResolutionDueAt`
    are `CreatedAt + policy.TargetHours`, full stop.

A2. **No auto-escalation, no auto-assignment, no notifications this slice.** `US-217`–`US-221`,
    `US-224`, `US-225` are cut. A breach is recorded (`SLAEvent`) and observable by reading tickets —
    acting on it (escalating, reassigning, notifying) is a second slice once the record it needs
    (this one) exists.

A3. **No pre-breach warning (`AC-5.6`).** Needs a configurable threshold percentage sourced from
    `PlatformSettings`, which this slice does not wire up. Cut with `US-217`.

A4. **"Paused" is approximated as "not evaluated while `Pending`, `Resolved` or `Closed`."**
    `US-213` (explicit pause/resume with a `PausedAt` column and `PausedSeconds` accounting) is not
    built. The breach job only evaluates tickets whose `Status` is `New` or `Open` — a ticket in
    `Pending` (typically: waiting on the customer) is not flagged as breaching, matching the spirit
    of `US-216`'s `TC-04` without the full pause/resume machinery. `SLAEvent.PausedSeconds` is stored
    as `0` always this slice — the column exists (per `US-211`'s schema) for `US-213` to fill in
    later, not computed here.

A5. **Policy matching: exact-priority, most-specific-wins.** A policy matches a ticket when
    `Priority` is equal and (`CategoryId` is null or equals the ticket's) and (`BranchId` is null or
    equals the ticket's). Since nothing yet assigns a ticket's `CategoryId`... — tickets already carry
    a required `CategoryId` (`FEAT-04`) but `BranchId` is still always null (`FEAT-16`, A1), so in
    practice only priority- and category-scoped policies can match today; branch-scoped ones become
    reachable once something assigns `Ticket.BranchId`. When more than one active policy matches, the
    one with more non-null criteria (`CategoryId`/`BranchId`) wins — a specific policy overrides a
    general one. No matching policy leaves both due dates `null` (`US-212`'s own `TC-02`).

A6. **The breach job is a `BackgroundService` polling loop, not a Hangfire recurring job.** Hangfire
    is registered in DI (`AddHangfireServer`) but nothing in this codebase actually schedules a
    recurring job through it — the one existing background worker (`NotificationSender`) is a plain
    `BackgroundService` on a fixed `Task.Delay` interval. This slice follows that proven, already-
    working pattern rather than introducing Hangfire's recurring-job API as this feature's first use.

A7. **SLA policy administration is Create + list only this slice.** No update, no deactivate — matches
    the minimum needed to configure and prove the feature; `US-214` (full policy CRUD) is not this
    slice's scope. Recorded as a cut, not an oversight.

## Out of scope

- `US-213` — explicit pause/resume (A4).
- `US-214` — full SLA policy CRUD beyond create + list (A7).
- `US-215` — business-hours calendar (A1).
- `US-217` — pre-breach warning notifications (A3).
- `US-218` — auto-escalation.
- `US-219` — SLA breach notifications (A2).
- `US-220` — auto-assignment.
- `US-221` — supervisor override.
- `US-222`, `US-223`, `US-224` — frontend (SLA countdown, policy admin UI, escalation badge). This
  slice is backend-only; a frontend slice follows once the API surface is proven, per this project's
  vertical-feature rule — but is deliberately not bundled into this already-large slice.
- `US-225` — ticket escalation state (`EscalationState` column). Not added this slice; nothing
  computes it without `US-218`.

## Acceptance criteria

AC-124. Given an SLA policy payload (`Priority`, `ResponseTargetHours`, `ResolutionTargetHours`,
optional `CategoryId`, optional `BranchId`), when an Admin creates it, then the policy is stored with
those fields and `IsActive = true`, and the response is `201`.

AC-125. Given a non-Admin caller, when creating an SLA policy, then the response is `403`.

AC-126. Given `ResponseTargetHours` or `ResolutionTargetHours` that is zero or negative, when
creating a policy, then the response is `400` keyed to the offending field.

AC-127. Given SLA policies exist, when an authenticated caller lists them, then the response is `200`
with the paged list.

AC-128. Given a ticket is created and an active SLA policy matches its priority (A5), then
`ResponseDueAt` and `ResolutionDueAt` are computed as `CreatedAt + target hours` (A1) and stored on
the ticket.

AC-129. Given a ticket is created and no active policy matches, then `ResponseDueAt` and
`ResolutionDueAt` remain `null` — ticket creation is not blocked by an absent policy.

AC-130. Given two active policies both match a ticket's priority, one scoped to the ticket's category
and one unscoped, then the category-scoped policy's targets are used (A5).

AC-131. Given a ticket whose `ResolutionDueAt` has passed, its `Status` is `New` or `Open`, and no
`SLAEvent` already records a resolution breach for it, when the breach-detection background service
runs, then an `SLAEvent` is recorded (`TargetType = "Resolution"`, `BreachedAt` set) — and the same
for `ResponseDueAt`/`TargetType = "Response"`.

AC-132. Given a ticket already has a recorded breach `SLAEvent` for a target type, when the
background service runs again, then no duplicate event is recorded for that ticket and target type.

AC-133. Given a ticket whose `Status` is `Pending`, `Resolved` or `Closed`, when the background
service runs, then it is not evaluated for breach regardless of its due dates (A4).

## Design

### Backend: Domain

**New:** `SLAPolicy : BaseEntity` (`Priority` string, `ResponseTargetHours`/`ResolutionTargetHours`
decimal, `CategoryId`/`BranchId` `Guid?`, `IsActive` bool) — same lookup-entity shape as
`Department`/`Branch` (`Create`, explicit `IsActive`).

**New:** `SLAEvent : BaseEntity, IAppendOnlyEntity` (`TicketId`, `TargetType` string
`"Response"`/`"Resolution"`, `TargetAt` DateTime, `BreachedAt` DateTime?, `PausedSeconds` int) —
append-only via the same `IAppendOnlyEntity` guard `TicketHistory`/`TicketMessage` already use
(`FEAT-14`'s generalisation pays for itself again here). A `Record(...)` factory, `Id` left
unassigned for the same EF Added-vs-Modified reason as every other append-only entity here.

**Edit:** `Ticket` gains `ResponseDueAt`, `ResolutionDueAt` (`DateTime?`, private setters) and a
`SetSlaTargets(DateTime? responseDueAt, DateTime? resolutionDueAt)` method, called once from
`Ticket.Create` — not retroactively callable, since nothing in this slice re-evaluates SLA on an
already-created ticket.

### Backend: Application

**New:** `CreateSLAPolicyCommand`/`Handler`/`Validator`, `GetSLAPoliciesQuery`/`Handler`/`Validator` —
same shape as `CreateDepartmentCommand`/`GetDepartmentsQuery`.

**Edit:** `CreateTicketCommandHandler` — after building the `Ticket`, looks up matching active
`SLAPolicy` rows (via a new `IRepository<SLAPolicy>.ListAsync` filtered on `Priority` and
`IsActive`), picks the most specific per A5, and calls `ticket.SetSlaTargets(...)` before
`AddAsync`/`SaveChanges`. No match: both nulls, ticket creation proceeds unchanged (AC-129).

### Backend: Infrastructure

**New:** `SlaBreachDetector : BackgroundService` in `Jobs/`, mirroring `NotificationSender`'s shape
(a scoped `AppDbContext`, a fixed polling interval — 1 minute, matching the existing worker — a
try/catch around one pass so one bad iteration does not kill the loop). Each pass:

1. Query tickets where `Status` is `New` or `Open`, and (`ResponseDueAt < now` or
   `ResolutionDueAt < now`).
2. For each, check (via `AppDbContext.Set<SLAEvent>().IgnoreQueryFilters()`) whether a breach event
   for that `TicketId`/`TargetType` already exists; skip if so (AC-132).
3. Otherwise append an `SLAEvent.Record(ticketId, targetType, targetAt, breachedAt: now)`.
4. One `SaveChangesAsync` per pass, matching `NotificationSender`'s batching.

Registered as a hosted service in `ServiceCollectionExtensions`, internal host only — same reasoning
`CategorySeeder`/`DepartmentBranchSeeder` restrict seeding to the internal host (`BASE-7`).

### Data model

One migration: `SLAPolicies`, `SLAEvents` tables, plus `ResponseDueAt`/`ResolutionDueAt` nullable
columns on `Tickets`. No changes to any other table.

### Error behavior

New codes `SLA_POLICY_CREATED`, plus `Validation.SLA_RESPONSE_TARGET_INVALID`/
`SLA_RESOLUTION_TARGET_INVALID` — each with a bilingual `Resources.yaml` pair, `SystemCode` /
`SystemCodeMap` entries (the `FEAT-16` lesson: a new failure code needs both, or it silently falls
back to 400 — the create/validation paths here are all 400-shaped, so this slice does not repeat the
404-mapping mistake, but the entries are still added for consistency and because a future story adds
a 404 path).
