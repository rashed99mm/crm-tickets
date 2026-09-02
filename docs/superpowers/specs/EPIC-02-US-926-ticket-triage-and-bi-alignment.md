# Ticket triage and BI alignment — the classification step a real service desk has

**Epic:** `EPIC-02` (lifecycle) with reporting in `EPIC-08` · **Feature:** `FEAT-33` ·
**Stories:** `US-926`…`US-929` · **Status:** draft — awaiting approval before any plan or code.

**Amends [`EPIC-02-US-922-ticket-domain-enrichment.md`](./EPIC-02-US-922-ticket-domain-enrichment.md)
(FEAT-32).** That feature put impact/urgency on the create form and defaulted customer-origin
tickets to `Medium`/`Medium`. This spec supersedes both decisions — see §Amendments — because a
requester cannot classify their own ticket and a fabricated classification is what makes the SLA
clock and the BI layer disagree.

## Problem

The product asks the wrong actor to classify a ticket, at the wrong moment, and then reports on the
answer as if it were true.

1. **The requester sets the classification.** `CreateTicketRequest` requires `impact`/`urgency`
   from whoever captures the ticket, and the portal path supplies neither, so
   `CreateTicketCommandHandler` defaults both to `Medium` (FEAT-32 `A2`) — deriving `Normal`. A
   customer who submits at 09:00 has silently had their priority decided by a fallback constant.
   In a real service desk, the requester describes the problem and **operations classifies it**:
   ask a requester and every ticket is urgent, which is precisely why ITIL splits impact and
   urgency and hands them to triage.

2. **SLA targets are computed once, from that fabricated value, and never recomputed.**
   `Ticket.SetSlaTargets` (`backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs:416`) has
   exactly one caller — `CreateTicketCommandHandler.cs:102`, inside `ApplySlaTargetsAsync`. FEAT-32
   added a `Reclassify` path that changes `Priority` and leaves `ResponseDueAt`/`ResolutionDueAt`
   frozen at whatever the guess produced. A ticket reclassified from `Normal` to `Urgent` keeps a
   `Normal` deadline.

3. **BI reads an incoherent mix of the two.** `GetSlaPerformanceReportQueryHandler.cs:36` groups by
   the ticket's *current* `Priority`, while the met/breached counts derive from due dates computed
   under its *original* priority (filter at `:23-26`). The moment anyone reclassifies, one row of
   "SLA attainment by priority" compares two different priorities.

4. **Nothing measures the queue that now matters most.** There is no record of when a ticket was
   classified, so "how long do tickets sit untriaged", "how good is triage's first call" and "is
   the matrix actually used" are unanswerable. An untriaged ticket has no SLA targets, so the
   breach scanner cannot see it either — the backlog is invisible to every existing surface.

## Decisions already made (with the human partner, 2026-08-31)

Recorded because each closed a branch the design would otherwise have to keep open:

- **A triage desk classifies before assignment.** Tickets arrive unclassified; classification is an
  operations act, not a capture field.
- **The SLA clock anchors at `CreatedAt` and is recomputed when classification changes.** The
  customer's clock starts when they asked, so triage delay eats into the target and stays visible
  instead of being hidden behind a later start.
- **Classification is the gate on `New → Open`** — no ninth state. `Open` finally means what
  EPIC-14's spec already claimed it meant ("triaged, not yet assigned").
- **`Priority` becomes nullable.** An unclassified ticket has no priority, and a null forces every
  consumer to handle that case; the rejected alternative was a fifth `Unclassified` priority value
  that SLA policies, filters and badges would each have to learn to ignore.
- **All four BI measures are in scope:** untriaged backlog and time to triage, SLA attainment by
  triaged priority, reclassification rate, impact×urgency distribution.

## Assumptions

Numbered, each written so it can be proven wrong.

- **A1.** **`Classify` is legal only from `New`.** Post-triage corrections go through the existing
  `Reclassify` path, which does not re-stamp `TriagedAt`. "When was this triaged" therefore has
  exactly one answer for the life of the ticket.
- **A2.** **Any authenticated staff user may triage, and only on the staff host.** No new role is
  introduced: `Supervisor` already exists for privileged acts, and inventing a `TriageAgent` role
  would need a permission, a seeder and an admin screen for a rule nobody has asked for. The
  `/triage` endpoint is declared on `InternalApi`'s `TicketsController` only — it is never reachable
  from the customer-facing `ExternalApi`, which is what stops a requester classifying their own
  ticket through the back door. Whether triage should be further restricted to a dedicated role is
  recorded as `OQ-T1`.
- **A3.** **Recompute re-adds accumulated pause.** `ApplySlaPauseTransition` shifts due dates
  forward by pause elapsed; a naive recompute from `CreatedAt` would discard it and silently
  shorten the target of a ticket that had been waiting on the customer. Recompute is therefore
  `AddBusinessHours(CreatedAt, targetHours, branch) + TotalPausedSeconds`, with a currently-open
  pause (`PausedAt` set) also counted to now.
- **A4.** **A recorded breach is history and is never withdrawn.** Recompute changes future
  evaluation only. De-escalating a ticket that already breached does not delete its `SLAEvent`; the
  scanner's existing duplicate-prevention keeps it from breaching twice.
- **A5.** **An unclassified ticket can never breach**, because it has no targets and the scanner
  only evaluates tickets that have them. No scanner change is needed; a test pins the behaviour so
  a future change to the scan predicate cannot quietly start breaching untriaged work.
- **A6.** **Historical tickets are grandfathered with `TriagedAt` null.** The migration fabricates
  no timestamps: tickets already past `New` keep their classification and simply have no triage
  time recorded, and only tickets *currently in* `New` have `Impact`/`Urgency`/`Priority` cleared so
  they re-enter triage for real. Consequence, stated rather than buried: time-to-triage and the
  reclassification rate measure only tickets triaged after this ships.
- **A7.** **Untriaged backlog is a live snapshot and ignores the report's date range.** Mixing a
  current queue depth into a historical window would make the number mean nothing; the screen
  labels it "as of now".
- **A8.** **Time to triage reports median alongside mean.** A few multi-day stragglers drag a mean
  away from what the desk experiences, and a single mean would flatter or damn the team by
  accident.
- **A9.** **The triage queue is `status=New`.** No new filter parameter: the existing status filter
  already expresses it, and a second way to ask the same question would drift.
- **A10.** **`GetSlaPerformanceReport` needs no rewrite.** Once recompute lands, targets and
  grouping refer to the same priority and the existing query is coherent on its own. What this
  feature owes it is a test that pins the coherence.
- **A11.** **Reclassification direction is derived from the priority ordering**
  (`Low < Normal < High < Urgent`) applied to the `FromValue`/`ToValue` of the `Reprioritized`
  history rows FEAT-32 already writes. No new schema.

## Out of scope

- A dedicated triage role or permission (A2, `OQ-T1`).
- Auto-classification rules (channel/keyword/VIP → default classification) — explicitly rejected in
  favour of a human triage desk, and a rule engine is its own feature.
- A separate triage SLA target with its own breach events; untriaged work is made visible through
  BI (§US-928), not through a second SLA policy dimension.
- Re-baselining the clock at triage time (considered and rejected: it hides customer-perceived
  delay).
- Backfilling triage timestamps for historical tickets (A6).
- CSAT, real-time/streaming BI, department/team drill-down — unchanged from the delivery plan.

## Acceptance criteria

Stable ids, permanent.

### US-926 — The triage gate (vertical)

- **AC-926.1** Given a ticket is created on either host, then `Impact`, `Urgency` and `Priority` are
  all null, `Status` is `New`, `TriagedAt`/`TriagedBy` are null, and no SLA targets are set.
- **AC-926.2** Given a request to `POST /api/tickets` carrying `impact`, `urgency` or `priority`,
  then those members have no effect: the created ticket is unclassified (the contract no longer
  declares them).
- **AC-926.3** Given a ticket in `New`, when `POST /api/tickets/{id}/triage` supplies a valid
  impact and urgency, then `Impact`/`Urgency` are stored, `Priority` is derived by
  `PriorityMatrix.Derive`, `TriagedAt`/`TriagedBy` are stamped, `Status` becomes `Open`, a `Triaged`
  history row is appended, and a `TicketStatusChangedEvent(New → Open)` is raised.
- **AC-926.4** Given a ticket in `New`, when `POST /api/tickets/{id}/status` attempts
  `New → Open`, then it is refused as the existing 409 — classification is the only door out of
  `New`.
- **AC-926.5** Given a ticket that is not in `New`, when `/triage` is called, then it is refused as
  a 409 (A1); given a ticket still in `New`, when `/classification` (reclassify) is called, then it
  is refused as a 409.
- **AC-926.6** Given a triage request with an impact or urgency outside `Low|Medium|High`, or with
  either absent, then the API returns the standard 400 envelope with an `errors[]` entry naming the
  field.
- **AC-926.7** Given the ticket list and detail endpoints, then `impact`, `urgency`, `priority`,
  `triagedAt` and `triagedBy` are all projected, with `priority` nullable on the wire.

### US-927 — SLA targets follow the classification (API-only)

- **AC-927.1** Given a ticket is created, then no SLA policy is matched and no targets are computed
  — `CreateTicketCommandHandler` no longer calls `SetSlaTargets`.
- **AC-927.2** Given a ticket is triaged and an active policy matches the derived priority (with
  the existing category/branch specificity rule), then `ResponseDueAt`/`ResolutionDueAt` are
  computed from **`CreatedAt`**, not from the triage time.
- **AC-927.3** Given a ticket is triaged and no active policy matches, then both targets stay null
  (the existing AC-129 behaviour, preserved).
- **AC-927.4** Given a triaged ticket is reclassified to a priority whose policy carries different
  targets, then both due dates are recomputed from `CreatedAt` under the new priority.
- **AC-927.5** Given a ticket accumulated paused time before being reclassified, then the
  recomputed due dates include `TotalPausedSeconds` (and any currently-open pause), so a
  reclassification never shortens the target of a ticket that waited on the customer (A3).
- **AC-927.6** Given a ticket already has a recorded breach `SLAEvent`, when it is reclassified —
  including a de-escalation that moves the due date past now — then the event is not deleted or
  altered (A4).
- **AC-927.7** Given an unclassified ticket older than any configured target, when the breach
  scanner runs, then it records nothing for that ticket (A5).
- **AC-927.8** Given the migration runs, then tickets currently in `New` have `Impact`, `Urgency`
  and `Priority` cleared, every other ticket keeps its stored values, and `TriagedAt` is null for
  all pre-existing rows (A6).

### US-928 — Triage BI (API-only)

- **AC-928.1** Given a Supervisor or Admin calls `GET /api/reports/triage?from&to`, then the
  response carries four blocks: `untriagedBacklog`, `timeToTriage`, `reclassification` and
  `classificationDistribution`.
- **AC-928.2** Given tickets in `New` of varying ages, then `untriagedBacklog` reports the total,
  counts in the buckets `<1h`, `1-4h`, `4-24h`, `>24h`, and the oldest ticket's `createdAt` —
  computed as of now and unaffected by `from`/`to` (A7).
- **AC-928.3** Given tickets triaged inside the range, then `timeToTriage` reports the count, the
  mean and the **median** minutes of `TriagedAt − CreatedAt` (A8); given none, then the block
  reports zero count with null statistics rather than a fabricated zero.
- **AC-928.4** Given tickets triaged inside the range, then `reclassification` reports how many
  carry at least one `Reprioritized` history row, that count as a share of the triaged total, and a
  split into escalated versus de-escalated by the priority ordering (A11).
- **AC-928.5** Given tickets triaged inside the range, then `classificationDistribution` reports a
  count per `(impact, urgency)` cell, with all nine cells present including zeros — an absent cell
  and a zero cell read identically on a heat grid and must not be conflated.
- **AC-928.6** Given an Agent (not Supervisor/Admin), when they call `/api/reports/triage`, then the
  request is refused by the existing `Supervisor` policy.
- **AC-928.7** Given a ticket triaged as `Normal` that breaches, and is then reclassified to
  `Urgent`, when `GET /api/reports/sla-performance` is read, then the ticket's row appears under
  `Urgent` **with due dates recomputed under `Urgent`** — the coherence A10 asserts, pinned by
  test rather than by a rewrite.

### US-929 — Triage on screen (frontend)

- **AC-929.1** Given the create form, then it carries no impact, urgency or priority control, and
  submitting it sends none.
- **AC-929.2** Given the ticket detail screen for a ticket in `New`, then the status card renders a
  Triage panel (impact select, urgency select, a live derived-priority preview, Triage action)
  **in place of** the "Move to" select, and no move control is offered. The preview is the same
  client-side mirror of the matrix FEAT-32 built for the create form, moved to where the decision
  is actually made.
- **AC-929.3** Given the Triage panel is submitted with both values, then it posts to `/triage` with
  the row version, and on success the screen re-reads and shows the derived priority, the new
  `Open` status and the move controls.
- **AC-929.4** Given a triaged ticket, then the detail screen shows the "Move to" select and the
  reclassify control, and no Triage panel.
- **AC-929.5** Given any screen rendering a ticket whose priority is null, then it renders as
  "Unclassified" rather than blank or a crash — queue row, detail header and dashboard alike.
- **AC-929.6** Given the ticket queue, then an "Untriaged" quick filter narrows it to `status=New`,
  and the untriaged count is visible without navigating.
- **AC-929.7** Given a Supervisor opens the Triage report screen, then all four blocks render as
  `AsyncState` views — backlog buckets, mean/median time to triage, reclassification split, and the
  3×3 distribution grid — with the backlog labelled "as of now"; the reports overview carries an
  untriaged-backlog tile linking to it.
- **AC-929.8** Given every new string on these surfaces, then it resolves through `| t` with an
  `en`/`ar` pair, and the layout is RTL-safe.

## Design

### Domain (`CustomerSupport.Domain`)

`Ticket` gains `TriagedAt`/`TriagedBy` (nullable), and `Priority` becomes `string?`. `Create` drops
its `impact`/`urgency` parameters — a third signature change to that factory in one working session,
which every fixture will feel, and the reason is worth the churn: capture and classification are
different acts by different people at different times.

```
Classify(impact, urgency, actorId)
    refuses unless Status == New                     // A1
    Impact/Urgency set, Priority = PriorityMatrix.Derive(...)
    TriagedAt = UtcNow, TriagedBy = actorId
    Status = Open
    Append(TicketChangeType.Triaged, null, priority)
    AddDomainEvent(TicketStatusChangedEvent(New, Open))
```

`ChangeStatus` gains one guard: a `New → Open` attempt throws `InvalidOperationException`, which the
handler's existing catch (added while fixing the AC-505 gap) turns into the standard 409. `TicketChangeType`
gains `Triaged`. Everything downstream of `Open` — the 12-pair table, the assignee guard, resolution
discipline, tags, links — is untouched.

### SLA recompute (`CustomerSupport.Application`)

The policy match and business-hours calculation move out of `CreateTicketCommandHandler`'s private
`ApplySlaTargetsAsync` into one application-layer service (`ISlaTargetCalculator`), called from
`TriageTicketCommandHandler` and `ReclassifyTicketCommandHandler`. It keeps the existing matching
rule (most specific active policy by priority, then category, then branch) and the existing
`IBusinessHoursCalculator`, and adds the pause re-add from A3. Create calls nothing.

### BI (`CustomerSupport.Application` + `InternalApi`)

One query, `GetTriageReportQuery`, returning `TriageReportDto` with the four blocks, behind the
existing `Supervisor` policy, aggregating in memory over `IRepository<Ticket>` and
`IRepository<TicketHistory>` in the same style as the other report handlers. No new schema: backlog
and distribution read `Status`/`Impact`/`Urgency`, time-to-triage reads `TriagedAt`, and the
reclassification split reads the `Reprioritized` history rows FEAT-32 already writes.

### Error behaviour

No new shapes. Malformed triage input is the existing 400 with field `errors[]`; wrong-state
refusals (`/triage` when not `New`, `/classification` when still `New`, `New → Open` via the status
route) are the existing 409. New message codes take the next free ranges — `VAL080+`, `ERR087+`,
`CON079+` — and every one is registered in all **four** places: `ApplicationErrors`, `SystemCode`,
`SystemCodeMap`, `Resources.yaml` (en + ar), plus `ResponseExtensions.MapFailureStatusCode` for any
code that must answer something other than 400. That fifth registration is called out because a
code missing from it silently answers 400 — the defect found in `ERR079` during FEAT-32.

### Frontend (`admin-app` + `common`)

`TicketApi`: `CreateTicketRequest` loses both fields; `triage(id, impact, urgency, rowVersion)` is
added; `TicketDetail`/`TicketListItem` gain `triagedAt`/`triagedBy` and take `priority: string | null`;
`reclassify` is unchanged on the wire. `derivePriority` stays — the Triage panel previews the derived
priority the same way the create form did, which is where that preview always belonged. The create
form loses its two selects and the preview chip; the detail screen swaps the move control for the
Triage panel while `New`; `CsBadge` renders a null priority as "Unclassified"; the queue gains an
Untriaged quick filter; reports gain the Triage screen and the overview tile.

### Testing

Domain unit tests for the gate (`Classify` from every non-`New` status refused, `New → Open` via
`ChangeStatus` refused, the derived priority and stamps), the recompute arithmetic including the
pause re-add, and the null-priority path. Integration tests for both endpoints, the migration's
effect on in-flight rows, the untriaged-never-breaches guarantee, the four BI blocks, and AC-928.7's
coherence check. Frontend component tests for the create form's absent controls, the Triage panel's
swap and post, the "Unclassified" rendering, and the report screen's four blocks. No new E2E journey
— S1's single-journey rule stands.

## Amendments to FEAT-32

Recorded here and marked in place in that spec rather than rewritten, so the order of decisions
stays legible:

| FEAT-32 item | Status | Why |
|---|---|---|
| `AC-923.1` (create requires impact/urgency) | **Superseded** by `AC-926.1`/`AC-926.2` | Capture is not classification |
| `A2` (customer-origin defaults to `Medium`/`Medium`) | **Deleted** | The fabricated default is the defect this feature removes |
| `AC-923.7` (create form's selects + preview) | **Superseded** by `AC-929.1`/`AC-929.2` | The controls move to the Triage panel |
| `AC-923.2`–`AC-923.6` (reclassify, matrix, DTOs) | **Retained** | Post-triage correction is still the only priority mutation path |
| `AC-922.x` (resolution), `AC-924.x` (tags), `AC-925.x` (links) | **Untouched** | Independent of classification |

## Open questions

- **`OQ-T1`.** Should triage be restricted to a dedicated role rather than any authenticated staff
  user (A2)? Deferred until someone states an operational reason; the change would be a policy on
  one endpoint.

## Traceability

`docs/assessment/brief.md` → this spec (`AC-926.x`…`AC-929.x`) → plan
`docs/superpowers/plans/EPIC-02-US-926-feat-33-ticket-triage-and-bi-alignment/` → tests naming each
AC → feature-complete commits. Stories `US-926`…`US-929` map 1:1 to the four AC blocks.
