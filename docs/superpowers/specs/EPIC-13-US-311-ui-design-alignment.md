# UI Design Alignment to Mockup Reference (Admin + Portal)

**Date:** 2026-08-26
**Status:** Draft for approval
**Type:** Frontend-only epic (design fidelity + portal build-out)
**Slices:** S1 (Ticket Lifecycle) scope, plus portal preview

## Context

The assessment brief delivers Slice S1 (the ticket lifecycle) end-to-end. The admin-app
(`admin-app`) is functionally complete — every routed page exists, is wired to the
`Response<T>` backend, and uses the shared Command Center design tokens (`theme.css`).
The portal-app (`portal-app`) is an empty scaffold: `routes = []`, zero feature pages,
only a shell with a branded header and language switcher.

The `stitch_smart_support_ticketing_crm/` folder holds 13 finished reference mockups
("the finished stories pages"). Comparing them to the running app surfaces two classes
of gap:

1. **Missing pages (portal).** The customer-facing app has no pages at all.
2. **Under-developed design (admin).** Functional pages render data but do not match the
   mockups' layout density: the dashboard shows only status-count tiles and a plain
   ticket list (no bento stat cards, no "recent active tickets" table, no activity feed);
   the ticket queue uses a CSS grid rather than the mockup's styled data table (mono
   reference, status pill with dot, customer column, relative "updated" time).

Both apps must be brought to the mockup reference design. The shared design language is
already established in `common/src/styles/theme.css` (Command Center tokens), so this
epic is about *composition and coverage*, not new tokens.

## Assumptions

- **A1 — Portal runs against the existing backend for this preview.** The external API
  (`CustomerSupport.ExternalApi`) is read-only and exposes only the knowledge base. A
  true customer portal (customer accounts, customer-scoped ticket endpoints, customer
  auth) is FEAT-17 (Sprint 10) and out of S1 scope. To deliver *visible, working* portal
  pages now without inventing that backend, the portal preview reuses the internal API
  surface (`/api/tickets`, `/api/customers`) through the existing `common` `TicketApi` /
  `CustomerApi` and the staff auth flow. This is a design/preview integration, recorded
  here as an assumption, not a claim that customer auth exists. The portal's
  `proxy.conf`/CORS will target the InternalApi host.
- **A2 — No new backend endpoints.** Every number and list the portal/admin needs already
  exists (ticket counts via `countOnly`, paged lists via `list`, ticket detail via
  `get`). Where the mockup shows data with no backing endpoint (CSAT, SLA breach rate,
  agent leaderboards, charts), those widgets are explicitly **out of scope** for S1 and
  are not built.
- **A3 — Design tokens are frozen.** `theme.css` is the single source of truth. New
  components use existing tokens; no new color/spacing/type tokens are introduced unless
  a mockup genuinely requires one (none identified).

## Acceptance criteria

### Admin fidelity

- **AC-A1** — The dashboard renders a bento row of stat cards (Open, Pending, Resolved
  Today, Assigned/My open) using counts already fetched, each card matching the mockup
  stat-card pattern (icon chip, uppercase `label-md` label, `display` number, optional
  delta). RTL-safe.
- **AC-A2** — The dashboard's "recent / my tickets" section renders as a styled data
  table (not a CSS grid) with columns: Reference (mono), Subject (truncated, two-line
  customer), Status (pill with dot), Updated (relative time), matching `user_dashboard`.
- **AC-A3** — The ticket queue renders as the same styled data table as AC-A2 (replacing
  the current grid), keeping its existing filters (status select, "my tickets" checkbox)
  and pagination.
- **AC-A4** — Ticket detail's activity history keeps its timeline but adopts the mockup's
  header band treatment (reference chip, priority badge, subject, opener byline, meta
  strip) already present; no regression. (Verification only — confirm it matches; extend
  only if a concrete gap is found during implementation.)
- **AC-A5** — All new/changed admin UI passes the common "no hardcoded strings" rule
  (every visible string goes through the `| t` pipe) and the existing component tests
  stay green.

### Portal build-out

- **AC-P1** — The portal-app registers routes and renders a shell with a sidebar nav
  (Submit Ticket, My Tickets, Knowledge Base, plus a Dashboard landing) matching the
  mockup nav style (brand block, active pill, sign-out), RTL-safe.
- **AC-P2** — A **Submit Ticket** page matches the `submit_ticket` mockup: `max-w-3xl`
  card, intro header, Subject / Department(Category) / Priority / Description fields,
  dashed attachment drop zone (visual only — upload wires to the existing attachment
  endpoint where one exists), Cancel + Submit actions. Submit posts to the ticket
  creation endpoint.
- **AC-P3** — A **My Tickets** list page matches the `ticket_queue` mockup: search bar,
  styled data table (Reference mono, Subject+Customer, Category, Priority pill, Status
  pill, Assignee), pagination. Reads the ticket list endpoint (customer-scoped in the
  future; for this preview, the same list filtered to the signed-in user).
- **AC-P4** — A **Ticket Detail** page for the portal reuses the admin detail's read
  view (description + activity) in the portal shell, customer-appropriate (no assign/
  status-transition controls that belong to agents).
- **AC-P5** — A **Knowledge Base** page lists articles from the external API
  (`/api/kb` / contents) — a real, read-only endpoint — matching the mockup's category
  grid + recent articles table at a basic level.
- **AC-P6** — Every portal string goes through `| t`; portal component tests are added
  and pass.

## Out of scope (explicitly)

- Customer accounts / customer auth (FEAT-17).
- CSAT, SLA, analytics, charts, agent leaderboards (FEAT-20, Sprints 13).
- AI assist sidebar (FEAT-21, Sprint 15).
- Conversation/reply composer persistence (FEAT-14, Sprint 6) — the ticket detail shows
  the existing activity read-only.
- New backend endpoints.

## Risks

- **R1** — Portal reusing staff auth/endpoints (A1) is a preview shortcut. Must be
  clearly documented so it is not mistaken for delivered customer auth.
- **R2** — Layout-only alignment can drift from behavior; ACs are pinned to existing
  endpoints to keep it honest.
