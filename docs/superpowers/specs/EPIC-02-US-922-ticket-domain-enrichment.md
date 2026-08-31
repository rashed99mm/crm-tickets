# Sprint 2 — Ticket domain enrichment: resolution discipline, impact/urgency matrix, tags, links

**Epic:** `EPIC-02` · **Feature:** `FEAT-32` · **Stories:** `US-922`…`US-925` ·
**Status:** draft — awaiting approval before any plan or code.

## Problem

The ticket aggregate carries a real lifecycle, SLA tracking and escalation, but four things every
production ticketing system records are missing:

1. **A resolved ticket carries no record of *how* it was resolved.** `ChangeStatus("Resolved")`
   stamps `ResolvedAt` and nothing else — no resolution code, no notes, no count of how many times
   the customer sent it back. A supervisor auditing resolutions has only a timestamp.
2. **Priority is a free pick.** Any agent selects `Low`–`Urgent` directly, so two identical
   incidents get different priorities depending on who typed them in. Real systems derive priority
   from **impact** (how many people/business functions are affected) and **urgency** (how fast it
   degrades), which is also what makes SLA targets defensible.
3. **No tags.** There is no lightweight way to label tickets (`billing`, `vip`, `password-reset`)
   and filter the queue by that label; category is a single taxonomy and cannot carry ad-hoc
   groupings.
4. **No relationships between tickets.** A duplicate is resolved with a comment saying "see
   TKT-000123" that nothing can follow; related incidents cannot be traversed.

## Decisions already made (with the human partner, 2026-08-31)

- Scope is these four enrichments, packaged as **one spec, one feature (`FEAT-32`), four vertical
  slices** delivered in cut order: resolution → matrix → tags → links. If time runs out the cut is a
  whole slice from the tail (links first, then tags), recorded in
  `docs/assessment/rubric-traceability.md` under Scope cuts.
- **Priority is matrix-only**: derived from impact × urgency, direct priority input removed from
  forms and refused by the API. No supervisor override.

## Assumptions

Numbered; each written so it can be proven wrong.

- **A1.** **Existing tickets are untouched by the matrix.** `Impact`/`Urgency` are nullable; every
  pre-existing ticket keeps its stored `Priority` until the first `Reclassify`. No backfill invents
  a classification nobody made.
- **A2.** **Customer-originated tickets default to `Medium`/`Medium` → `Normal`.** Portal
  submissions and channel-ingested tickets (WhatsApp/SMS/web form paths) do not ask the customer
  for impact/urgency; the default equals today's default priority (`Normal`), so their behaviour
  does not change.
- **A3.** **Resolution is required on every transition into `Resolved`** — from `In Progress` and
  from `Open` alike (both are legal per the FEAT-28 transition table). One rule, no special case.
- **A4.** **Reopening clears `ResolutionCode`/`ResolutionNotes` and increments `ReopenCount`**, in
  the same place reopen already clears `ResolvedAt`/`ClosedAt`. There is no cap on reopens — a cap
  is a policy decision nobody has made.
- **A5.** **The missing-resolution refusal is a 400, not a 409.** The request is malformed (fields
  absent), so the existing validator pipeline reports field-level `errors[]` the form can place on
  controls. The aggregate carries a second guard that throws (`InvalidOperationException` → 409) so
  no future handler can bypass the rule; in practice the validator fires first.
- **A6.** **Tag normalization:** trim, collapse internal whitespace to one space, lowercase with
  the invariant culture. 1–30 chars after normalization; Unicode letters (Arabic included), digits,
  dash and space only; at most **10 tags per ticket**; duplicates (post-normalization) refused.
  Free-form — no curated tag dictionary this sprint.
- **A7.** **Links are single rows.** `RelatedTo` is stored once and displayed on both tickets;
  `DuplicateOf` is directional (source *is a duplicate of* target). Only the direct two-ticket
  cycle (A duplicates B while B duplicates A) is refused; longer chains are legal — chain detection
  is graph traversal this sprint does not need.
- **A8.** **`Duplicate` resolution code requires an existing `DuplicateOf` link** on the ticket
  being resolved (a 409 — the request is well-formed, the state is wrong). Creating the link never
  auto-resolves the ticket.
- **A9.** **History gets three new change types** — `Reprioritized`, `TagAdded`, `TagRemoved` — and
  no more. Resolution rides on the existing `StatusChanged`/`Reopened` rows; links carry their own
  `CreatedBy`/`CreatedAt` and do not write history rows.
- **A10.** **The create-ticket contract breaks, deliberately.** `priority` leaves the request body;
  `impact`/`urgency` enter as required fields. The inherited create tests are updated inside slice
  2, not in a later cleanup.
- **A11.** **No new E2E journey.** S1's single-journey rule stands; every criterion here is served
  by unit, integration and component tests.

## Out of scope

- Actual ticket **merging** (moving messages/history between tickets) — `DuplicateOf` is a pointer,
  not a merge.
- A curated tag dictionary, tag administration screen, or tag-based reporting.
- Supervisor priority override (decided against — matrix only).
- Exposing impact/urgency to portal customers (A2).
- Any change to SLA policy matching — it keeps reading the derived `Priority` string.
- Reopen caps or auto-close of stale `Resolved` tickets.

## Acceptance criteria

Stable ids, permanent. Convention: `AC-<story>.n`, one block per story/slice.

### US-922 — Resolution discipline (slice 1, vertical)

- **AC-922.1** Given a status-change request targeting `Resolved` without `resolutionCode` or
  without `resolutionNotes`, then the API returns the standard 400 envelope with an `errors[]`
  entry naming each missing field, and the ticket does not change.
- **AC-922.2** Given a status-change request targeting `Resolved` with a valid code
  (`Fixed | Workaround | Duplicate | CannotReproduce | NoResponse`) and notes (≤ 2000 chars), then
  the ticket stores both, `ResolvedAt` is stamped, and history records `StatusChanged` as today.
- **AC-922.3** Given a `resolutionCode` outside the five values, then the API returns 400 with the
  field named; the domain value object refuses it independently.
- **AC-922.4** Given a `Resolved`/`Closed` ticket is reopened, then `ResolutionCode` and
  `ResolutionNotes` are cleared, `ReopenCount` increments by exactly 1, and the existing `Reopened`
  history row still records the transition.
- **AC-922.5** Given any code path calls `Ticket.ChangeStatus` into `Resolved` without resolution
  details, then the aggregate throws `InvalidOperationException` (defense in depth behind AC-922.1).
- **AC-922.6** Given the ticket detail endpoint, then the DTO carries `resolutionCode`,
  `resolutionNotes` and `reopenCount` (null/0 until set).
- **AC-922.7** Given the admin ticket detail screen, then choosing the `Resolved` transition opens
  an inline form (code select + notes textarea) instead of committing bare; server `errors[]` land
  on the named controls; a resolved ticket shows a banner with its code and notes; `reopenCount > 0`
  is visible. All strings en/ar, RTL-safe.

### US-923 — Impact/urgency → derived priority (slice 2, vertical)

- **AC-923.1** Given a create-ticket request, then `impact` and `urgency` (`Low | Medium | High`)
  are required, `priority` is no longer accepted, and the stored priority is derived exactly per
  the matrix below — all nine cells unit-tested.
- **AC-923.2** Given a reclassify request on an existing ticket with new impact/urgency, then the
  priority is re-derived; when the derived value changes, history records a `Reprioritized` row
  with the old and new priority.
- **AC-923.3** Given any write surface (create, update), then `priority` is absent from every
  request contract — a request body carrying a `priority` value has no effect on the stored
  priority (integration-tested: send it, assert the derived value stands).
- **AC-923.4** Given the migration runs on existing data, then every existing ticket keeps its
  stored `Priority`, `Impact`/`Urgency` are null, and SLA policy matching behaviour is unchanged
  (regression: an existing integration test over priority-matched SLA still passes).
- **AC-923.5** Given a portal-submitted or channel-ingested ticket, then it is created with
  `Medium`/`Medium` → `Normal` (A2) without asking the customer.
- **AC-923.6** Given ticket list and detail endpoints, then DTOs carry `impact` and `urgency`.
- **AC-923.7** Given the admin create form, then impact/urgency selects replace the priority
  select and a non-editable derived-priority preview updates as they change (client mirror of the
  matrix, display only — the server value is authoritative).

**The matrix** (impact rows × urgency columns → existing `TicketPriority` values):

| Impact \ Urgency | Low | Medium | High |
|---|---|---|---|
| **Low** | Low | Low | Normal |
| **Medium** | Low | Normal | High |
| **High** | Normal | High | Urgent |

### US-924 — Tags (slice 3, vertical)

- **AC-924.1** Given an add-tag request, then the value is normalized per A6 and refused with a 400
  naming the field when empty after normalization, over 30 chars, outside the allowed charset, a
  duplicate of an existing tag on the ticket, or the 11th tag.
- **AC-924.2** Given an Arabic-script tag (e.g. `فوترة`), then it is accepted, stored and returned
  intact.
- **AC-924.3** Given a tag is added or removed, then history records `TagAdded`/`TagRemoved` with
  the normalized value.
- **AC-924.4** Given `GET /api/tickets?tag=<value>`, then only tickets carrying the normalized tag
  are returned, filtered server-side, composable with the existing status/priority/mine filters.
- **AC-924.5** Given the ticket detail screen, then tags render as removable chips with an add
  input that surfaces the limit and validation errors; given the queue, then a tag filter sits
  beside the existing filters and rows show their tags.

### US-925 — Related / duplicate links (slice 4, vertical)

- **AC-925.1** Given a create-link request with type `RelatedTo` or `DuplicateOf` and an existing
  target ticket, then the link is stored once with `CreatedBy`/`CreatedAt`; self-links, unknown
  targets and exact duplicates (same source, target, type) are refused.
- **AC-925.2** Given ticket A already `DuplicateOf` B, when B attempts `DuplicateOf` A, then the
  request is refused as a 409 (direct cycle, A7).
- **AC-925.3** Given a ticket without a `DuplicateOf` link, when resolved with code `Duplicate`,
  then the API returns 409 and the ticket does not change; with the link present the same request
  succeeds.
- **AC-925.4** Given a delete-link request by id, then the link is removed; deleting a link on a
  ticket already resolved as `Duplicate` is allowed (the resolution stands — history is not
  rewritten).
- **AC-925.5** Given the ticket detail endpoint and screen, then `links[]` (type, direction, other
  ticket's reference and subject) render as a section with an add control (target found by
  reference) and per-row remove; `RelatedTo` appears on both tickets, `DuplicateOf` reads
  directionally ("duplicate of TKT-…" / "duplicated by TKT-…").

## Design

### Domain (`CustomerSupport.Domain` — no new dependencies)

- **New value objects:** `ResolutionCode` (five values), `TicketImpact`, `TicketUrgency`
  (`Low|Medium|High`), mirroring `TicketPriority`'s sealed-class pattern.
- **`PriorityMatrix`** — a static pure function `Derive(TicketImpact, TicketUrgency) → TicketPriority`
  in Domain (it is a business rule, not a service with dependencies).
- **`Ticket`** gains `ResolutionCode`, `ResolutionNotes`, `ReopenCount`, `Impact`, `Urgency`
  (private setters, as every field there):
  - `ChangeStatus` gains an optional `ResolutionDetails` parameter (code + notes record); entering
    `Resolved` without it throws; reopen clears it and increments `ReopenCount` beside the existing
    `ResolvedAt`/`ClosedAt` clearing.
  - `Create` takes impact + urgency, derives priority; the `priority` parameter is removed.
  - New `Reclassify(impact, urgency, actorId)`; `UpdateDetails` loses its `priority` parameter.
  - New `AddTag(value, actorId)` / `RemoveTag(value, actorId)` over a `TicketTag` child collection
    (same append pattern as `TicketHistory`).
- **`TicketTag`** — child entity: `TicketId`, `Value`. Normalization lives in a
  `TagValue` value-object factory so the rule is stated once.
- **`TicketLink`** — entity: `SourceTicketId`, `TargetTicketId`, `LinkType` (VO:
  `RelatedTo|DuplicateOf`), audit fields. Cross-aggregate guards (target exists, cycle, duplicate
  row) live in the command handler — the aggregate cannot see other tickets.
- **`TicketChangeType`** gains `Reprioritized`, `TagAdded`, `TagRemoved` (A9).

### Persistence (`CustomerSupport.Infrastructure`)

One migration, `Sprint2TicketEnrichment`:
- `Tickets`: `Impact` (nvarchar, null), `Urgency` (null), `ResolutionCode` (null),
  `ResolutionNotes` (nvarchar(2000), null), `ReopenCount` (int, not null, default 0).
- `TicketTags`: FK to `Tickets`, unique index `(TicketId, Value)`.
- `TicketLinks`: FKs to `Tickets` (source/target), unique index
  `(SourceTicketId, TargetTicketId, LinkType)`.
No data backfill (A1).

### API (`CustomerSupport.Api.Shared` / `InternalApi`)

| Change | Surface |
|---|---|
| Changed | `POST /api/tickets` — `impact`+`urgency` required, `priority` removed |
| Changed | status-change endpoint — optional `resolutionCode`+`resolutionNotes`, required for `Resolved` |
| Changed | the existing ticket-update endpoint — `impact`+`urgency` (optional, both-or-neither) replace `priority`; supplying both triggers `Reclassify`. No new endpoint. |
| New | `POST /api/tickets/{id}/tags` · `DELETE /api/tickets/{id}/tags/{value}` |
| New | `POST /api/tickets/{id}/links` · `DELETE /api/tickets/{id}/links/{linkId}` |
| Changed | `GET /api/tickets` — `tag=` filter |
| Changed | list DTO: `impact`, `urgency`, `tags[]`; detail DTO adds `resolutionCode`, `resolutionNotes`, `reopenCount`, `links[]` |

Authorization: identical to today's ticket mutations — the same handler-level rules that guard
status change and assignment guard these; nothing new is invented. External (customer) host gets
**no** new write surface.

### Error behaviour

No new shapes. Malformed input → existing 400 envelope with field `errors[]` (FluentValidation
pipeline). Wrong-state refusals (`Duplicate` without link, direct cycle, aggregate guards) →
existing `InvalidOperationException` → 409 path.

### Frontend (`frontend/projects/admin-app` + `common`)

- Shared label maps for impact/urgency/resolution codes beside the existing status model — single
  source of truth, all strings through `| t` (en/ar), RTL-safe.
- Create form: impact/urgency selects + derived-priority preview chip; priority control removed;
  server field errors on controls (the contract check FEAT-04 established).
- Detail: resolve inline form; resolution banner; reopen count; tag chip editor; links section.
- Queue: tag filter + tag chips on rows.
- `AsyncState` loading/empty/error conventions throughout; no new chart or utility dependency.

### Testing

- **Domain (xUnit):** nine matrix cells; resolution guard; reopen clear+increment; tag
  normalization/charset/limit/duplicate; `ResolutionCode` VO refusals. Each test names its AC in
  `[Trait("AC", "…")]`.
- **Integration (`WebApplicationFactory`):** each endpoint row above — including the 400 envelope
  shape for a bare resolve, the 409 for `Duplicate` without link, the cycle refusal, the `tag=`
  filter, and the AC-923.4 SLA regression.
- **Frontend (component):** resolve form validation/submit, derived-priority preview, tag chips,
  create-form server-error placement.
- **No new Playwright journey (A11).**

## Traceability

`docs/assessment/brief.md` → this spec (`AC-922.x`…`AC-925.x`) → plan
`docs/superpowers/plans/EPIC-02-US-922-feat-32-ticket-domain-enrichment/` → tests naming each AC →
four feature-complete commits, one per slice. Stories `US-922`…`US-925` map 1:1 to the four AC
blocks and carry the frontend/backend split in their `Ships with` rows.
