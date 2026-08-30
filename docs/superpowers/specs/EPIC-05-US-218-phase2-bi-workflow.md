# Phase 2 — Real-life ticket workflow, BI layer, domain enrichment, UX redesign

**Epic:** `EPIC-14` · **Features:** `FEAT-28`, `FEAT-29`, `FEAT-30` · **Stories:** `US-901`…`US-918` ·
**Status:** partially implemented — lifecycle/SLA slices are present; BI enrichment and UX criteria
remain tracked below against the current codebase.

## Current implementation status

| Area | Current evidence | State |
|---|---|---|
| Ticket lifecycle | `TicketStatus.cs`, `TicketStatusTests`, `TicketLifecycleEndpointTests` | Implemented: eight statuses and 12 legal transitions |
| SLA pause/resume | `Ticket.cs`, `SlaPauseAndEscalationEndpointTests` | Implemented: both waiting states pause and resume the clock |
| SLA breach scan | `SlaBreachScanner.cs`, `SlaBreachDetector.cs`, `SlaTrackingEndpointTests` | Implemented: `New`/`Open` scan, five-minute configurable worker, duplicate prevention |
| Domain enrichment | `Phase2Enrichment` migration and related entities | Implemented in persistence; verify population/filter UX separately |
| Executive dashboard API | No `ExecutiveDashboard` query/controller currently exists | Not implemented |
| BI/UX acceptance criteria | Existing report and screen implementations | Partial; criteria remain pending until the named endpoint and UI behavior exist |

This status table is authoritative for review: acceptance criteria describe the target, while the
state column records what is actually present in source and tests.

## Problem

Three things a real support operation needs that the product cannot do today:

1. **The ticket lifecycle now has real-life granularity.** The implemented machine is
   `New → Open → Assigned → In Progress → Waiting for Customer / Waiting for Internal Team →
   Resolved → Closed` (`backend/src/CustomerSupport.Domain/ValueObjects/TicketStatus.cs`).
   The supplied BI & Workflow Specification defines this richer lifecycle the operation actually runs —
   `New → Open → Assigned → In Progress → Waiting for Customer / Waiting for Internal Team →
   Resolved → Closed` — with waiting-on-anyone not counting against SLA and an escalation thread that
   names the owner.
2. **The BI layer cannot answer the specification's KPI catalogue.** Reports exist for volume, SLA
   attainment and agent performance (`ReportsController`, `US-601…604`), but first-response time and
   handle time are **approximations** (spec `EPIC-08-US-606-reporting.md` A5, A7) because no
   lifecycle timestamps exist; there is no executive dashboard; and the Org→Branch→Dept→Team→Agent→
   Ticket drill-down chain cannot work because **no Team exists** and the existing
   `DepartmentId`/`BranchId`/`TeamId` columns are never populated by any code path.
3. **The UI reads as "dead HTML"** — silent immediate-commit status `<select>`s, plain customer/
   category pickers with no type-ahead, a customer-profile screen holding ten pieces of data the
   product doesn't even store, a permanently-unavailable tickets lane, `window.confirm` and
   hardcoded English in KB admin, report screens as bare lists. The screens do not behave like a
   tool that follows the workflow they display.

## Assumptions

Numbered; each is a question that was not asked directly, written so it can be proven wrong.

- **A1.** **`Escalated` is rendered as a marker (`EscalationState`), not a ninth status.** The
  specification's lifecycle diagram shows an `Escalated` branch off any active state. The codebase
  already carries escalation as a parallel field with levels `None/Warning/Level1/Level2/Level3`
  (`Ticket.EscalationState`, driven by the SLA breach scanner + `AdvanceEscalation`); turning it
  into a status would fork the transition table into two incompatible machines and break every
  status-gated query. The workflow's *behaviour* — an escalated ticket is handled by a named
  Supervisor/Specialist — is captured by adding **`Ticket.EscalationAssigneeId`** and moving the
  ticket back onto the main thread (`In Progress`) with the marker visible.
- **A2.** **Both waiting states pause SLA.** The spec's rule is "time spent waiting on the customer
  **or on an internal team** is not counted against the SLA". `ApplySlaPauseTransition`
  (`Ticket.cs`) explicitly keys the pause on `Waiting for Customer` and `Waiting for Internal Team`.
- **A3.** **The reopen target is `In Progress`, not `Open`.** `IsReopenTo` and the
  `Resolved`/`Closed` transitions in `TicketStatus.cs` reopen where work resumes. `Open` remains a
  real stage (triaged, not yet assigned).
- **A4.** **Assignment is required before work begins.** Transitions into `In Progress` and the two
  waiting states are refused when `AssigneeId` is null. This is the "who is doing this" business
  rule the workflow implies.
- **A5.** **`FirstResponseAt`/`LastResponseAt` are stamped when an outbound `TicketMessage` is
  recorded**, in `RecordTicketMessageCommandHandler` (FEAT-14), keyed on `Direction == "Outbound"`.
  This replaces the A5 approximation: the timestamp is owned by the aggregate, not derived from the
  message table at query time. `ResolvedAt`/`ClosedAt` are stamped on the transitions into those
  statuses and cleared on reopen.
- **A6.** **The `Team` entity is flat — no hierarchy of teams inside departments.** Org→Branch→Dept→
  **Team**→Agent is the drill-down depth the spec names; teams themselves do not nest.
- **A7.** **Org-chain wiring populates existing dormant columns** (`Ticket.DepartmentId/BranchId`,
  `ApplicationUser.DepartmentId/BranchId`) rather than introducing new ones. Ticket inherits
  department/branch from its assignee on assign, and at creation from the acting agent's own when
  the agent has them. `ApplicationUser.TeamId`/`Ticket.TeamId` are new.
- **A8.** **CSAT stays cut** (`EPIC-08-US-606-reporting.md` A2). There is no rating-collection
  mechanism: the backend `GetCsatReport` reads a `SurveyResponse` table nothing writes, the portal
  survey stories (US-408/409/415) are unwired. The BI catalogue lists CSAT as **not answerable** and
  the executive dashboard shows the CSAT card in the non-interactive unavailable state rather than
  inventing data.
- **A9.** **Real-time BI stays cut.** Reporting reads committed tables (`SLAEvent`, `Ticket`,
  `TicketMessage`, `SurveyResponse`), not an event stream. The spec's event model is honoured only
  in that domain events already exist (`TicketCreatedEvent`, `TicketStatusChangedEvent`,
  `TicketAssignedEvent`); no streaming sink is added.
- **A10.** **The BI catalogue is built against what the schema can answer, and everything else is
  listed with its blocker, not silently omitted.** A KPI a report can't yet produce has a named
  reason (see §BI) so a reviewer sees the whole catalogue.
- **A11.** **Drill-down ships for the dimensions the reports actually have** — date range (ed), and
  ticket attributes the volume report already groups by. Department/Team drill-down is blocked until
  the enrichment wiring (A7) populates real, non-null data; this epic populates the data *for
  future* filters but does not ship a filter over it.

## Out of scope

- CSAT report + survey collection (A8).
- Real-time/streaming BI (A9).
- Team hierarchy depth beyond one level (A6).
- A 9th `Escalated` status (A1).
- ERP connectors, AI chatbot, mobile apps (unchanged from delivery plan).
- The 12-phase feature surface outside tickets (customers, KB, AI, integrations are separate epics;
  this epic cites them as context only).
- `US-609` export (already cut — no CSV/Excel dependency).
- Storydept tasks 16–21 (RTL/Arabic, branding, API-key auth, tenancy) — done in their own sprint,
  in parallel.

## Acceptance criteria

Stable ids, permanent. Blocks: `AC-501`…`AC-536`.

### Workflow — the 8-state lifecycle (`FEAT-28`)

AC-501. Given a ticket in a status among the eight, when a transition listed in the table below is
attempted, then `ChangeStatus` applies it, history records `StatusChanged` (or `Reopened` for a
reopen), and a `TicketStatusChangedEvent` is raised.

AC-502. Given a ticket in any status, when a transition *not* in the table is attempted (including a
no-op to its own status), then `ChangeStatus` throws `InvalidOperationException` and the status does
not change (surfaced as the existing `409`).

AC-503. Given a ticket in `Resolved` or `Closed`, when reopened, then it moves to `In Progress`
(A3), `TicketHistory` records `Reopened` with `FromValue` = the closed/resolved status and
`ToValue = "In Progress"`, and `ResolvedAt`/`ClosedAt` are cleared.

AC-504. Given a ticket in `In Progress`, when moved to `Waiting for Customer` or `Waiting for
Internal Team`, then the SLA pause starts (`PausedAt` set); given a ticket leaving a waiting state,
then the elapsed span accumulates into `TotalPausedSeconds` and both `ResponseDueAt`/`ResolutionDueAt`
shift forward by the span (A2).

AC-505. Given a ticket with no `AssigneeId`, when moved to `In Progress`, `Waiting for Customer` or
`Waiting for Internal Team`, then `ChangeStatus` throws (403-class refusal, A4); given it has an
assignee, the transition is allowed.

AC-506. Given a Supervisor/Specialist takes ownership of an escalated ticket, then
`Ticket.EscalationAssigneeId` is set, history records an `Escalated` row, and the ticket's
`EscalationState` reflects the level (A1).

AC-507. Given any screen rendering a ticket, then the escalated marker is shown from
`EscalationState`/`EscalationAssigneeId` (never as a 9th status option in a status picker).

**The transition table** (every other pair refused; diagonal empty):

| From \ To | Open | Assigned | In Progress | Wait Customer | Wait Internal | Resolved | Closed |
|---|---|---|---|---|---|---|---|
| New | ✓ | | | | | | |
| Open | | ✓ | | | | ✓ | |
| Assigned | | | ✓ | | | | |
| In Progress | | | | ✓ | ✓ | ✓ | |
| Wait Customer | | | ✓ | | | | |
| Wait Internal | | | ✓ | | | | |
| Resolved | | | ✓ (reopen) | | | | ✓ |
| Closed | | | ✓ (reopen) | | | | |

`In Progress`/waiting transitions require an assignee (AC-505).

### Domain enrichment (`FEAT-28`)

AC-508. Given the organisation, then a `Team` entity exists with `Name`, `DepartmentId`, `ManagerId`,
`IsActive`, unique name within its department, and CRUD + deactivate tested.

AC-509. Given the schema, then `ApplicationUser.TeamId` and `Ticket.TeamId` exist as nullable FKs to
`Teams`, with a migration that backfills existing rows to null and keeps all existing FKs valid.

AC-510. Given tickets over their lifecycle, then `Ticket` carries `FirstResponseAt`, `LastResponseAt`,
`ResolvedAt`, `ClosedAt` stamped per A5, surfaced in the ticket DTOs.

AC-511. Given an agent creates a ticket and the acting agent has a department/branch, then the ticket
inherits them at creation; given a ticket is assigned, then it inherits the assignee's
department/branch/team; dormant columns are populated (A7).

AC-512. Given the BI hierarchy, then Org→Branch→Dept→Team→Agent→Ticket can be traversed from
non-null data for users/tickets that have been wired (A7, A11).

### BI layer (`FEAT-29`)

AC-513. Given a Supervisor or Admin calls `GET /api/reports/executive-dashboard?from&to`, then the
response contains, for the range: `ticketsCreated`, `openNow`, `unassigned`, `breachedSla`,
`timeToFirstResponseMinutes` (mean), `resolutionRate` (resolved/created), `avgHandleMinutes` (A5/A7
replaced where timestamps exist).

AC-514. Given the executive dashboard screen, when it loads for the default range, then it renders
the summary cards, a pods/sparkline list for volume over the range, an SLA attainment bar per
priority, and a top-agents list — each card an `AsyncState` view, never fabricated data.

AC-515. Given a Supervisor or Admin, when they set a from/to range on the dashboard, then the screen
re-fetches with those parameters and the range is reflected in the URL (reusing the existing shared
date-range filter behaviour).

AC-516. Given an Agent (not Supervisor/Admin), when they navigate to the dashboard/reports, then the
routes are not reachable (same `Supervisor` policy gate as AC-148).

AC-517. Given the KPI catalogue (below), then every KPI is either (a) implemented by a shipped query
with a named source, or (b) listed as not-answerable with its blocker — no KPI is silently missing.

AC-518. Given `FirstResponseAt`/`ResolvedAt` exist, then the agent-performance report's
`avgHandleMinutes` uses `ResolvedAt - CreatedAt` (paused-adjusted) instead of
`UpdatedAt - CreatedAt` (replaces A7), and responses use `FirstResponseAt - CreatedAt`.

AC-519. Given the escalation thread, then an escalation KPI (tickets escalated within a range,
share of created) can be computed from `EscalationState != "None"` without new schema.

AC-520. Given report screens, then the ticket-volume report renders its three breakdowns as visual
bar/behavioural components (not bare lists) and the live-queue/dashboard surfaces show the standard
date filter where a range applies.

### UX redesign (`FEAT-30`)

AC-521. Given the ticket queue, then it has a working search/sort/filter surface (status, priority,
assignee, escalation) with results server-filtered, rows navigate to detail, and the existing
`mine`/`unassigned` filters are honoured.

AC-522. Given the ticket detail screen, then status changes are explicit: the current status is
shown, available transitions are offered as buttons/confirmed action (never a silent immediate-commit
`<select>`), a success/failure toast confirms the outcome, and a 409 path reloads instead of silently
losing state.

AC-523. Given the ticket create form, then customer and category are type-ahead searchable pickers
(trie/filter over an API-aware source), and server field errors land on the control, not a banner.

AC-524. Given the ticket detail messages section, then the conversation is conversation-first
(messages directly under the header, oldest-first, composer anchored below, clearer inbound/outbound
visual direction) rather than a buried card.

AC-525. Given an agent's dashboard, then "my work" matches the workflow: unassigned/assigned/in
progress/waiting-on-me states drive the sections, with SLA countdown and an activity strip — all from
real data.

AC-526. Given the customer profile, then no fabricated data is shown: real fields (name, contact,
id, created) always render, the dead tickets lane is gone or wired to a real filtered list, and the
ten not-stored fields render as a compact "not recorded" group rather than ten grey lanes.

AC-527. Given the reports screens, then volume/SLA/agent reports render as visual components
(inline SVG bars/pods, no chart library dependency added — A20 below constrains it) with a shared
date-range filter, and the executive dashboard links to them.

AC-528. Given KB admin, then `window.confirm` and hardcoded English are gone: destructive actions
use the existing inline-confirm pattern and all strings go through `| t`.

AC-529. Given the portal home and dashboard, then the portal dashboard is not a static menu: it shows
the signed-in customer's real open tickets, quick submit, and KB entry from live data.

AC-530. Given any redesigned screen, then loading/empty/error states are visually distinct
(`AsyncState` convention) and empty never looks like failure.

AC-531. Given any redesigned screen, then all strings are translated (en/ar) and the layout is
RTL-safe, consistent with the shipped localisation conventions.

AC-532. Given statuses, then the frontend status model lists exactly the eight statuses with tints
for each, and every `<select>`/pill uses the shared model (single source of truth, no scattered
string literals).

AC-533. Given the assigned-to-me workflow, then an agent can self-assign from the queue/detail
without supervisor action (matching the workflow's manual assignment alternative), governed by the
existing role rules.

AC-534. Given escalation, then an escalated ticket surfaces an escalation banner naming the level and
owner, and a Supervisor can hand it to a Specialist from the detail screen.

AC-535. Given the admin shell, then navigation reflects the workflow: dashboard, tickets (queue),
customers, reports (executive + volume + SLA + agent), KB, admin — no dead nav entries and no route
offering views that 401.

**A20.** No charting library will be added in this epic (`frontend/package.json`); visualizations use
inline SVG/Tailwind so the build stays dependency-free and the mockup palette stays the source.

## Design

### The 8-state machine

`TicketStatus` (TicketStatus.cs) defines exactly `New`, `Open`, `Assigned`, `In Progress`, `Waiting
for Customer`, `Waiting for Internal Team`, `Resolved`, and `Closed`; `Pending` is rejected and is
migration. `CanTransitionTo` implements the table above.
Reopen detection (`IsReopenTo`) retargets to `In Progress`. Because statuses persist as strings,
this is a domain/persisted-value change, not a schema-column change; no status-rewrite migration is
present in the current migrations.

`Ticket` (Ticket.cs) changes:
- `ChangeStatus` keeps the refusal-throwing contract but accrues the new guard: entering work states
  (AC-505) requires `AssigneeId != null`.
- `ApplySlaPauseTransition` keys on the two waiting statuses instead of the literal `"Pending"`.
- New timestamps `FirstResponseAt`, `LastResponseAt`, `ResolvedAt`, `ClosedAt` with a method
  `RecordResponse(DateTime)` and set/clear on transitions into and out of `Resolved`/`Closed`.
- New `EscalationAssigneeId` (nullable Guid) + `TakeEscalation(Guid specialistId, Guid actorId)`.

`RecordTicketMessageCommandHandler` (FEAT-14) stamps `FirstResponseAt` on the first outbound and
`LastResponseAt` on every outbound — via a new `Ticket.RecordResponse(DateTime)`, loaded tracked
(`GetTrackedAsync`) or `tickets.Update`.

### Domain enrichment

New `Team` entity mirroring `Department` (same shape: `Name`, `ManagerId`, `IsActive`, `Deactivate`,
well-known-id `Create` for seeding). FKs: `ApplicationUser.TeamId`, `Ticket.TeamId`. Seeder adds one
default team per existing department (e.g. "General Department Team") so the hierarchy has real rows.

### BI catalogue — every KPI, answered or blocked

| KPI | Ship? | Source / blocker |
|---|---|---|
| Ticket volume by period/category/priority | ✅ (exists AC-149…151) | `Ticket.CreatedAt` |
| SLA attainment (first response / resolution) | ✅ (exists AC-152) | `SLAEvent` (met = target & no breach) |
| Agent throughput + handle time | ✅ (exists AC-153, improved AC-518) | `Ticket.ResolvedAt` replaces A7 |
| First response time | ✅ (AC-510 → replace A5) | `Ticket.FirstResponseAt` |
| Open / unassigned / breached counts now | ✅ (AC-513) | `Ticket.Status/EscalationState` |
| Escalation rate | ✅ (AC-519) | `EscalationState != None` |
| CSAT, NPS | ❌ A8 | no rating collection |
| Realtime dwell, live throughput | ❌ A9 | stream plumbing absent |
| Department/Team drill-down | ⚠️ A11 (data wired, UI filter deferred) | population begins AC-511 |

### API surface

**New endpoint (FEAT-29):** `GET /api/reports/executive-dashboard?from&to` behind the existing
`Supervisor` policy, returning an `ExecutiveDashboardDto`. Implemented as a query handler over
`IRepository<Ticket>`/`IRepository<SLAEvent>`/`IRepository<TicketMessage>` using the existing
in-memory aggregation style (no new repository methods). **Adapted (FEAT-28):** `agent-performance`
switches `avgHandleMinutes` to the timestamp field; `TicketListItemDto`/`TicketDetailDto` gain the
new fields.

### Error behaviour

No new failure codes: every refusal here is the existing `409` (state conflicts + concurrency) via
the established `InvalidOperationException`→`ToActionResult` path, or `400` via the existing
validators. The enrichment and lifecycle tests name their AC in `[Trait("AC", "...")]`.

## Traceability

`brief.md → AC-501…536 (this spec) → task files in
docs/superpowers/plans/2026-08-28-feat-28-… | 29 | 30 → tests naming the AC → feature-complete
commits`. Stories US-901…918 map 1:1 to criterion clusters in their `Ships with` rows.
