# EPIC-06 · Knowledge Base — Stories (All Slices)

| Epic | Slice(s) | BRD Requirements |
|---|---|---|
| `EPIC-06` | S4 | FR-6.1–FR-6.8 |

---

## S4 — Article Lifecycle

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-501 | `Publish` command (Draft → Published) | Backend | S4 | M | `done` | FR-6.1 |
| US-501 | `Archive` command (Published → Archived) | Backend | S4 | M | `done` | FR-6.1 |
| US-501 | Dedicated Publish/Archive endpoints | Backend | S4 | M | `done` | FR-6.1 |
| US-502 | Article versioning (version number + change history) | Backend | S4 | S | `done` | FR-6.8 |
| US-502 | Who changed what, when (field-level audit on articles) | Backend | S4 | S | `done` | FR-6.8 |

---

## S4 — Article Organisation

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-503 | Category/tag taxonomy + public tree endpoint | Backend | S4 | M | `done` | FR-6.2 |
| US-504 | Curated FAQ list (distinct from full article set) | Backend | S4 | M | `done` | FR-6.3 |
| US-504 | FAQ endpoint (published FAQs only) | Backend | S4 | M | `done` | FR-6.3 |

---

## S4 — Article-to-Ticket Linking

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-505 | Link article to ticket as applied solution | Backend | S4 | M | `done` | FR-6.5 |
| US-505 | Applied-solution count per article | Backend | S4 | S | `done` | FR-6.7 |

---

## S4 — Search

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-506 | Arabic-aware search (diacritic folding) | Backend | S4 | M | `done` | FR-6.4 |
| US-506 | Search endpoint with bilingual results | Backend | S4 | M | `done` | FR-6.4 |

---

## S4 — Analytics

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-507 | Record article view (increment ViewCount) | Backend | S4 | S | `done` | FR-6.7 |
| US-508 | Helpfulness vote (Like/Dislike) | Backend | S4 | S | `done` | FR-6.7 |
| US-508 | View + helpfulness analytics endpoint | Backend | S4 | S | `done` | FR-6.7 |

---

## S4 — Customer Exposure

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| — | Published articles only to customers | Backend | S4 | M | `done` | FR-6.6 |
| US-513 | Portal: browse + filter by category (wired 2026-08-29) | Frontend | S4 | M | `done` | FR-6.6 |
| US-513 | Portal: search articles | Frontend | S4 | M | `done` | FR-6.4, FR-6.6 |

---

## S4 — Admin Frontend

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-509 | KB admin: article list | Frontend | S4 | M | `not started` | FR-6.1 |
| US-510 | KB admin: create article form | Frontend | S4 | M | `not started` | FR-6.1 |
| US-511 | KB admin: edit article | Frontend | S4 | M | `not started` | FR-6.1, FR-6.8 |
| US-512 | KB admin: publish/archive actions | Frontend | S4 | M | `not started` | FR-6.1 |
| US-504 | KB admin: FAQ management | Frontend | S4 | M | `not started` | FR-6.3 |
| US-505 | KB admin: link article to ticket | Frontend | S4 | S | `not started` | FR-6.5 |

---

## Summary

| Category | Total Stories | Done | Not Started |
|---|---|---|---|
| Lifecycle | 5 | 5 | 0 |
| Organisation | 3 | 3 | 0 |
| Linking | 2 | 2 | 0 |
| Search | 2 | 2 | 0 |
| Analytics | 3 | 3 | 0 |
| Customer Exposure | 3 | 3 | 0 |
| Admin Frontend | 6 | 0 | 6 |
| **Total** | **24** | **18** | **6** |

Status corrected 2026-08-29: backend FEAT-11 schema fully shipped via prior migrations (ContentCategory taxonomy, ContentVersion, ContentView, ContentVote, ContentTicketLink, Publish/Archive/SetFaq/LinkToTicket commands, Arabic diacritic search); portal browse wired to real categories with active filter breadcrumb; public `GET /api/knowledge-base/categories` added today; anonymous vote replaced with sign-in CTA on article detail.
