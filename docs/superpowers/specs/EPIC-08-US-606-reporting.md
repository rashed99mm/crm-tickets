# Reporting — ticket volume, SLA performance, agent performance

**Sprint:** 13 · **Feature:** `FEAT-19`+ (roadmap's provisional number for Sprint 13 is unassigned;
using the next free slot after `FEAT-21`) · **Stories:** `US-601`, `US-602`, `US-603`, `US-604`,
`US-608` (adapted) · **Epic:** `EPIC-08` Reports & Management

## Problem

Nothing today answers "how much work came in," "did we meet our SLA commitments," or "how is each
agent doing" — three questions any manager needs and none of which existed a screen for before this
session's SLA work (`FEAT-17`) gave the platform real breach data to report on.

## Assumptions

A1. **No `Manager` role, no department JWT claim — `US-608` is built against what exists, not what
    the story assumes.** The story's role vocabulary (`Manager`) and scoping mechanism (a
    `departmentId` claim, "admin sees all, manager sees own department") don't exist anywhere in
    this codebase: the only roles are `Admin`, `Supervisor`, `Agent` and four inherited-platform
    roles (`ApplicationRole.Roles`), and nothing issues a department claim. Worse, department
    *scoping* would be scoping over data that's always null: `Ticket.DepartmentId` and
    `ApplicationUser.DepartmentId` both exist (`FEAT-16`) but nothing has ever assigned either —
    building a filter over a column that is always `NULL` for every row is not a smaller version of
    `US-608`, it is dead code with a passing test that proves nothing. What ships instead: reports
    are gated to `Admin`/`Supervisor` at the controller level (the same split `TicketsController`
    already uses for other manager-shaped actions), with no department filter at all.

A2. **No CSAT report (`US-605`).** There is no rating-collection mechanism anywhere in this
    codebase — no `CustomerSatisfaction` entity, no survey trigger, nothing a report could read.
    Cut entirely, not approximated.

A3. **No export (`US-609`).** Needs a CSV/Excel-writing capability this codebase doesn't have
    (no `CsvHelper`/OpenXML package in `Directory.Packages.props`), and depends on `US-610`
    (frontend filter UI), also not built this pass. Cut.

A4. **No frontend (`US-606` management dashboard, `US-607` live queue dashboard, `US-610` filter
    UI) in the original pass.** Backend-only that slice, per this project's own rule that a
    vertical slice can legitimately stop at the API when the UI is real, separate scope — recorded
    here rather than silently deferred. **Superseded 2026-08-27 by the addendum below**: `US-606`
    and `US-607` as written need a `/api/reports/dashboard` endpoint, a live-queue endpoint, CSAT
    data and branch-scoped filtering — none of which exist (A1, A2). Rather than build that
    additional backend now, the addendum ships three report screens against the endpoints that
    actually exist (`US-602`/`603`/`604`), with `US-610` narrowed to the one filter dimension every
    endpoint actually accepts: date range. This is the same adapt-to-what-exists call already made
    for `US-608` in A1, not a new pattern.

A5. **"First response" is approximated as the ticket's earliest outbound `TicketMessage`
    (`FEAT-14`).** No column anywhere stores an explicit "first response sent at" timestamp; the
    conversation record already has exactly this fact. A ticket with no outbound message has no
    first-response time and is excluded from the first-response half of the SLA report, not treated
    as a breach or a miss.

A6. **"Met" vs "breached" for the SLA report reads `SLAEvent`, not a live timestamp comparison.**
    `FEAT-17`'s breach scanner is the single source of truth for "did this breach" — a ticket with a
    due date and no recorded `SLAEvent` for that target type is counted as met; one with a recorded
    breach event is counted as breached. This is simpler and more consistent than re-deriving breach
    status from raw timestamps in the report query, and it means the report and the breach scanner
    can never disagree about what breached.

A7. **Agent performance's "average handle time" uses `Ticket.UpdatedAt - Ticket.CreatedAt`** for
    tickets in `Resolved`/`Closed` status, not a precise "time of the resolving status change" —
    that value would need scanning `TicketHistory` for each ticket's `StatusChanged`-to-`Resolved`
    entry, which is a real query cost this pass does not take on. Recorded as an approximation: a
    ticket resolved and then reopened and resolved again would show a longer "handle time" than the
    time actually spent resolving it, because `UpdatedAt` reflects the *last* change, not the first
    resolution.

A8. **Ticket volume's "grouped by day/week/month" ships as three independent breakdowns
    (by period, by category, by priority) over the same filtered date range, not one fully
    cross-tabulated table.** `US-602`'s three ACs each test one dimension independently; a full
    period×category×priority cross-tab is a different, heavier report nobody asked for yet.

## Out of scope

- `US-605` (CSAT report) — A2.
- `US-609` (export) — A3.
- `US-606`, `US-607`, `US-610` (frontend) — A4.
- Department-scoped report filtering — A1.
- Business-hours-aware "handle time" — A7.

## Acceptance criteria

AC-148. Given an Admin or Supervisor, when they call any `/api/reports/*` endpoint, then it
succeeds; given anyone else (including an unauthenticated caller), then the response is `403`/`401`.

AC-149. Given tickets created across several days within `[from, to]`, when
`GET /api/reports/ticket-volume?from=...&to=...` is called, then the response's `byPeriod` breakdown
groups counts by the requested `groupBy` (`day`/`week`/`month`, default `day`).

AC-150. Given tickets across multiple categories within range, then `byCategory` shows the correct
per-category counts.

AC-151. Given tickets across multiple priorities within range, then `byPriority` shows the correct
per-priority counts.

AC-152. Given tickets with a `ResponseDueAt`/`ResolutionDueAt` set (a matching `SLAPolicy` existed at
creation) and created within `[from, to]`, when `GET /api/reports/sla-performance` is called, then
the response groups by priority with `total`, `metFirstResponse`, `breachedFirstResponse`,
`metResolution`, `breachedResolution` — `met + breached = total` for each target type, per A6.

AC-153. Given resolved/closed tickets assigned to agents within `[from, to]`,
when `GET /api/reports/agent-performance` is called, then the response lists each agent with
`ticketsResolved` and `avgHandleMinutes` (A7).

AC-154. Given an out-of-order date range (`from` after `to`), any report endpoint returns `400`
keyed to the field.

## Design

### Backend: Application

**New:** `Features/Reports/Dtos/` — `TicketVolumeReportDto` (`ByPeriod`/`ByCategory`/`ByPriority`,
each `IReadOnlyList<(string Key, int Count)>`-shaped records), `SlaPerformanceReportDto`
(`IReadOnlyList<SlaPerformanceRow>`, one row per priority), `AgentPerformanceReportDto`
(`IReadOnlyList<AgentPerformanceRow>`).

**New:** `Features/Reports/Queries/GetTicketVolumeReport/`, `GetSlaPerformanceReport/`,
`GetAgentPerformanceReport/` — one query + handler + validator each, `IRepository<Ticket>`/
`IRepository<SLAEvent>`/`IRepository<TicketMessage>`-driven, no new repository methods (existing
`ListAsync`/`CountAsync` cover every shape needed).

### Backend: API

**New:** `ReportsController`, `[Authorize(Policy = "Supervisor")]` at the controller level (the
existing `Supervisor` policy already means "Supervisor or Admin" per `ADR-0012`'s role hierarchy —
confirm this against `AddPlatformAuthorization`'s actual policy definition before relying on it;
if `Supervisor` is not already inclusive of `Admin`, use `[Authorize(Roles = "Admin,Supervisor")]`
explicitly instead). Three `GET` actions: `ticket-volume`, `sla-performance`, `agent-performance`,
each taking `from`/`to` (required) and `groupBy` (ticket-volume only, optional).

### Data model

No schema changes — every report reads existing tables.

### Error behavior

New validation codes for `from > to`; no new `SystemCode`/`SystemCodeMap` entries needed since every
failure path here is a 400 (`VAL001`), not a new 404/409 shape — the `FEAT-16` lesson about
registering new *failure* codes doesn't apply because this feature introduces none.

---

## Addendum (2026-08-27): frontend — adapted `US-606`/`US-607`/`US-610`

**Stories:** `US-606`, `US-607` (both adapted, see A4), `US-610` (narrowed to date-range only).
Written because this feature's backend shipped without ever producing a saved
`implementation-plan.md` referencing this reporting spec's frontend addendum before this point —
this addendum and its plan now precede the frontend code they describe, closing that gap for the
new work (the backend gap itself is recorded separately, unchanged).

### Problem

Three report endpoints exist and are fully tested, but nothing renders them — a Supervisor or Admin
who wants to answer "how much work came in" or "who's carrying the load" has to call the API
directly. `US-606`/`US-607` as written ask for aggregated dashboards this codebase's data can't yet
answer (CSAT, live per-agent load, branch scoping); what it *can* answer is exactly the three reports
already built.

### Out of scope (beyond A1–A4 above)

- CSAT summary card (A2 — no data source).
- Live queue / unassigned-tickets view (`US-607` as written — no `assignedAgentId`/branch schema
  match; would need its own spec against this codebase's actual `Ticket.AssigneeId`/`Status`
  shape, not adapted here).
- Category/priority/branch *filter controls* (`US-610` AC2–AC4) — category and priority are
  **breakdown dimensions already returned by** `GET /api/reports/ticket-volume`, not filters
  layered on top; branch filtering doesn't exist per A1. Only the date-range filter (`US-610` AC1)
  is real, shared filter UI.
- CSV/Excel export (`US-609`, A3) — unchanged, still cut.

### Acceptance criteria

AC-160. Given a Supervisor or Admin navigates to the reports section, when the ticket volume report
loads for the default date range, then it renders three breakdowns (`byPeriod`, `byCategory`,
`byPriority`) as returned by `GET /api/reports/ticket-volume`, and a `groupBy` control (day/week/
month) re-fetches `byPeriod` on change.

AC-161. Given the SLA performance report, when it loads, then it renders one row per priority with
total/met/breached counts for both first-response and resolution targets, as returned by
`GET /api/reports/sla-performance`.

AC-162. Given the agent performance report, when it loads, then it renders one row per agent with
tickets resolved and average handle minutes, as returned by `GET /api/reports/agent-performance`.

AC-163. Given any of the three report screens, when the user sets a from/to date range and applies
it, then the report re-fetches with those query parameters (`US-610` AC1) and the range is reflected
in the URL's query parameters for shareability.

AC-164. Given an Agent (not Admin/Supervisor) navigates to the reports section, then the screens are
not reachable — the route is guarded the same way `AuditLogComponent`'s route already is, matching
the backend's `Supervisor`-policy gate (AC-148).

### Design

**Frontend — new files (admin-app):**
- `TicketVolumeReportComponent`, `SlaPerformanceReportComponent`, `AgentPerformanceReportComponent`
  — one per endpoint, each a filterable table/breakdown view following `AuditLogComponent`'s
  filterable-table shape and this project's `AsyncState<T>` convention (loading/loaded/empty/error,
  never rendering a failed load as "no data").
- `ReportDateRangeFilter` (`common`) — a small shared component (from/to date pickers + apply),
  reused by all three screens (AC-163), reflecting its value in the URL's query params.
- `ReportsApi` (`common`) — typed client methods for the three existing endpoints, matching this
  project's established API-client and envelope-unwrapping pattern.
- Routing: three routes under the admin shell, `Supervisor`-role-guarded (AC-164), with a "Reports"
  nav entry.

### Error behavior

No new backend error paths. `AC-154`'s `from > to` 400 is surfaced by the existing envelope
interceptor exactly as every other validation error already is; no new frontend error-mapping code.
