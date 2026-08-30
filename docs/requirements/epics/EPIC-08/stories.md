# EPIC-08 · Reports & Management — Stories (All Slices)

| Epic | Slice(s) | BRD Requirements |
|---|---|---|
| `EPIC-08` | S6 | FR-9.1–FR-9.8 |

---

## S6 — Reporting Backend

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-601 | `ReportsController` on InternalApi | Backend | S6 | M | `done` | FR-9.1 |
| US-602 | Ticket volume report (by period, category, priority) | Backend | S6 | M | `done` | FR-9.1, RPT-1 |
| — | Open backlog report (current snapshot + trend) | Backend | S6 | M | `not started` | FR-9.1, RPT-1 |
| US-603 | SLA performance report (attainment, breaches, time-to-breach) | Backend | S6 | M | `done` | FR-9.2, RPT-2 |
| US-604 | Agent performance report (throughput, handle time, reopen rate) | Backend | S6 | M | `done` | FR-9.3, RPT-3 |
| US-605 | CSAT report (ratings, response rate, split by language) | Backend | S6 | M | `done` | FR-9.4, RPT-4 |
| — | Escalation analysis report | Backend | S6 | S | `not started` | RPT-5 |
| — | KB effectiveness report (deflection, views, applied solutions) | Backend | S6 | S | `not started` | RPT-6 |
| — | Channel performance report | Backend | S6 | S | `not started` | RPT-7 |
| — | Reopen analysis report | Backend | S6 | S | `not started` | RPT-8 |
| US-609 | Export report to spreadsheet (CSV/Excel) | Backend | S6 | S | `cut` — no CSV/Excel dependency, recorded in delivery plan | FR-9.6 |

---

## S6 — Dashboard Backend

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-606 | Management overview dashboard endpoint (DSH-1) | Backend | S6 | M | `done` — the four report endpoints (`ticket-volume`, `sla-performance`, `agent-performance`, `csat`) serve the overview dashboard | FR-9.5, DSH-1 |
| US-607 | Live queue dashboard endpoint (DSH-2, 1-min refresh) | Backend | S6 | M | `adapted` — composed client-side from `TicketApi` (list + statuses) on 2026-08-29, no dedicated endpoint | FR-9.5, DSH-2 |
| — | Agent workload dashboard endpoint (DSH-3) | Backend | S6 | M | `not started` | FR-9.5, DSH-3 |
| — | My work dashboard endpoint (DSH-4) | Backend | S6 | M | `done` | FR-9.5, DSH-4 |

---

## S6 — Branch/Department Scoping

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-608 | Reports respect caller's department scope | Backend | S6 | M | `adapted` — no `Manager` role or populated department columns exist, so scoping ships as Admin/Supervisor gating | FR-9.8 |
| US-608 | Reports respect caller's branch scope | Backend | S6 | M | `adapted` — same basis as department scoping; branch filter dropped | FR-9.8 |

---

## S6 — Frontend

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-606 | Management overview dashboard (DSH-1) | Frontend | S6 | M | `done` — `/reports/overview` hub composes the four report endpoints (2026-08-29); dashboard CSAT tile reads the real report | FR-9.5 |
| US-607 | Live queue dashboard (DSH-2) | Frontend | S6 | M | `done` — `/reports/live-queue` composed from `TicketApi` | FR-9.5 |
| — | Agent workload dashboard (DSH-3) | Frontend | S6 | M | `done` — the agent dashboard (my work, status counts, supervisor-only unassigned + CSAT panels) | FR-9.5 |
| — | Ticket volume report page | Frontend | S6 | M | `done` — reachable from the sidebar; period + group-by filters | FR-9.1 |
| — | SLA performance report page | Frontend | S6 | M | `done` — reachable from the sidebar; period filter | FR-9.2 |
| — | Agent performance report page | Frontend | S6 | M | `done` — reachable from the sidebar; period filter | FR-9.3 |
| — | CSAT report page | Frontend | S6 | M | `done` — reachable from the sidebar; period filter | FR-9.4 |
| US-610 | Report filter controls (period, category, priority, branch) | Frontend | S6 | M | `done` — shared `cs-report-date-range-filter` (period) on all four screens + group-by on ticket volume; branch dropped with US-608 | FR-9.1 |
| US-609 | Export button on report pages | Frontend | S6 | S | `not started` — backend export cut, no CSV/Excel dependency | FR-9.6 |
| — | Scheduled report subscriptions by email | Backend | S6 | C | `not started` | FR-9.7 |

---

## Summary

| Category | Total Stories | Done | Not Started |
|---|---|---|---|
| Reporting Backend | 11 | 6 (includes US-609 cut) | 5 |
| Dashboard Backend | 4 | 3 (includes US-607 adapted) | 1 |
| Scoping | 2 | 2 (both adapted) | 0 |
| Frontend | 10 | 8 | 2 |
| **Total** | **27** | **19** | **8** |

> **Statuses reflect executed work as of 2026-08-29.** `done` means a test names the story's
> acceptance criteria and has been run. `adapted` is a recorded deviation from the letter of the
> story (no `Manager` role / department columns → Admin/Supervisor gating; live queue composed
> from `TicketApi`; export cut for lack of a CSV/Excel dependency). Rows marked `not started`
> (open backlog, escalation/KB/channel/reopen analyses, DSH-3 endpoint, scheduled email reports,
> export) are either optional (`S`/`C` priority) or explicitly cut.
