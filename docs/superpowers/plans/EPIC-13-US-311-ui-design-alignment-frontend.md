# Plan: UI Design Alignment to Mockup Reference

**Spec:** `docs/superpowers/specs/EPIC-13-US-311-ui-design-alignment.md`
**Date:** 2026-08-26
**Approach:** Frontend-only, TDD (failing test first), per-feature.

## Feature 1 — Admin dashboard fidelity (AC-A1, AC-A2, AC-A5)

**Why:** The dashboard is the landing screen and currently the thinnest page vs. the
`user_dashboard` / `agent_dashboard_overview` mockups.

**Tasks:**
1. Add a `CsStatCard` presentational component to `common` (icon, label, value, optional
   delta, optional href) — used by dashboard and reusable. *(AC-A1)*
   - Test: renders label/value/icon; delta slot; `t` pipe for all strings.
2. Refactor `DashboardComponent` to render a bento grid of `CsStatCard`s (Open, Pending,
   Resolved Today, My open) from the counts it already fetches. *(AC-A1)*
   - Test: four stat cards appear with correct counts and labels.
3. Replace the dashboard "my work" CSS-grid list with a shared `CsDataTable`
   presentational component (Reference mono, Subject two-line, Status pill w/ dot,
   Updated relative). *(AC-A2)*
   - Test: table renders rows; status pill shows dot+label; empty/loading/error branches.
4. Extract `CsDataTable` + `CsStatusPill` into `common` (also consumed by ticket queue).
   - Test: pill maps each status to its token color + dot; table columns configurable.

## Feature 2 — Admin ticket queue table (AC-A3, AC-A5)

**Why:** Queue currently uses a CSS grid; mockup uses a styled table.

**Tasks:**
1. Swap `TicketQueueComponent`'s grid for `CsDataTable` (Reference, Subject+Customer,
   Category, Priority pill, Status pill, Assignee). Keep filters + pagination. *(AC-A3)*
   - Test: rows render; filters still drive the query; pagination unchanged.
2. Add `CsPriorityPill` (mirrors `CsStatusPill`) using `--color-priority-*`.
   - Test: maps Low/Normal/High/Urgent to tokens.

## Feature 3 — Portal shell + nav (AC-P1)

**Why:** Portal is empty — the single biggest missing-page gap.

**Tasks:**
1. Add portal routes (`app.routes.ts`): `/` → Dashboard, `/tickets/new`, `/tickets`,
   `/tickets/:id`, `/kb`.
2. Build `PortalShell` sidebar nav matching mockup (brand block, nav items with active
   pill, sign-out) reusing `cs-icon`, `cs-language-switcher`, `| t`. *(AC-P1)*
   - Test: nav renders items; active route gets pill; sign-out calls session.

## Feature 4 — Portal Submit Ticket (AC-P2, AC-P6)

**Tasks:**
1. `PortalSubmitTicketComponent`: form (Subject, Category select, Priority select,
   Description, dashed attachment drop zone visual) → posts via `TicketApi.create`.
   *(AC-P2)*
   - Test: validation errors map; success navigates to `/tickets`; submit disabled while busy.

## Feature 5 — Portal My Tickets + Detail (AC-P3, AC-P4, AC-P6)

**Tasks:**
1. `PortalTicketListComponent`: search + `CsDataTable` (Reference mono, Subject+Customer,
   Category, Priority pill, Status pill, Assignee) + pagination, reading ticket list.
   *(AC-P3)*
   - Test: rows render; search drives query.
2. `PortalTicketDetailComponent`: read-only detail (description + activity timeline) in
   portal shell, no agent controls. *(AC-P4)*
   - Test: renders description + history; no assign/transition controls present.

## Feature 6 — Portal Knowledge Base (AC-P5, AC-P6)

**Tasks:**
1. `PortalKbComponent`: lists articles from external API contents endpoint (read-only).
   *(AC-P5)*
   - Test: renders article list; loading/error/empty branches.

## Verification

- `cd frontend && npx ng test common --watch=false` — green (new `CsStatCard`,
  `CsDataTable`, `CsStatusPill`, `CsPriorityPill` tests).
- `cd frontend && npx ng test admin-app --watch=false` — green (dashboard + queue).
- `cd frontend && npx ng test portal-app --watch=false` — green (new portal components).
- `cd frontend && npx ng build admin-app` and `npx ng build portal-app` — clean.
- Manual: run both hosts, log in, confirm dashboard bento + tables, and portal pages
  render aligned to mockups (RTL toggle checked).

## Order

F1 → F2 (admin, backend-supported, highest visual payoff) → F3 → F4 → F5 → F6 (portal).
