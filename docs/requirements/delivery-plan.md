# Delivery plan

> **Amended 2026-08-25 — the backend baseline changed.** The CCE Platform reference was adopted as
> the CRM backend ([ADR-0009](../adr/0009-adopt-the-support-platform-as-the-crm-baseline.md)), so this
> plan no longer describes work starting from an empty solution.
>
> **What that changes.** `FEAT-01` (platform foundation) is delivered by the adopted platform, and so
> in large part are the knowledge base, notifications, settings and integrations — features this plan
> had scheduled for sprints 11, 8, 12 and 9. **What it does not change is the critical path:** the
> reference is CMS-shaped and has no tickets, so `FEAT-04`, `FEAT-05`, `FEAT-06`, `FEAT-07` and
> `FEAT-08` — capture, queue, detail, lifecycle, assignment, history — remain the whole of the
> remaining work and are now the first thing to build, not the fourth.
>
> The sprint sequencing below and its dependency reasoning still hold; the starting point moved.
> Re-planning it properly is worth doing once the ticket workflow lands, not before.


Sprint sequencing for the whole product. This file owns **feature grouping, sprint assignment,
ordering, estimates and delivery status** — the planning layer the rule specification's
documentation structure does not name explicitly. Slice identities come from
[`../assessment/brief.md`](../assessment/brief.md); story ids are global (`US-nnn`) and owned by
[`./user-stories/`](./user-stories/) + [`./epics/`](./epics/); criteria ids stay spec-owned.

## The unit of delivery is a feature, not a layer

**A feature ships as backend + frontend + tests, together, or it has not shipped.** Work is not
organised into a backend phase followed by a frontend phase. Each `FEAT-nn` below is a vertical
increment: the API, the screen that uses it, and the tests for both, finished before the next
feature starts.

The loop, per feature — normative definition in
[`.claude/skills/sdd-workflow/SKILL.md`](../../.claude/skills/sdd-workflow/SKILL.md):

```
spec (already approved)
   ↓
backend plan  →  backend implementation (TDD)
   ↓
frontend plan  →  frontend implementation (TDD)      ← written when the backend plan completes,
   ↓                                                   not deferred to a later sprint
tests green at every level the feature touches
   ↓
ship: feature-complete commit
   ↓
next feature
```

**Why this replaced the layered plan.** The previous arrangement gave sprint 4 the entire agent
application, so every screen waited behind every endpoint. That hides integration risk until the
end: an envelope shape, an error contract or a field name that the frontend cannot actually consume
is discovered three sprints after it was decided, when the cost of changing it is highest. Shipping
vertically moves that discovery into the feature that caused it.

### Not every feature is vertical, and the exceptions are recorded

| Kind | Features | Why |
|---|---|---|
| **Vertical** — backend + frontend | `FEAT-02`, `FEAT-04`, `FEAT-05`, `FEAT-06`, `FEAT-12`, `FEAT-13` | The normal case |
| **API-only** | `FEAT-01`, `FEAT-03`, `FEAT-07`, `FEAT-08`, `FEAT-09` | Either infrastructure with no user surface, or a backend capability whose UI the spec locates in another feature's screen |
| **Frontend-only** | `FEAT-10`, `FEAT-11` | Cross-cutting behaviour and the terminal journey; their server halves shipped earlier |

`FEAT-03` (customer records) is the one that deserves scrutiny. It is API-only **because the S1 spec
defines no frontend criterion for customer management screens** — customers surface in the UI only
through the ticket create form's picker and through the notes and attachments screens of `FEAT-12`
and `FEAT-13`. That is a gap in the spec rather than a decision of this plan, and it is raised as
`G-5` in [`../assessment/rubric-traceability.md`](../assessment/rubric-traceability.md). Nothing here
invents criteria to fill it.

`FEAT-07` and `FEAT-08` are API-only for a different and less worrying reason: their user surface is
real but lives inside `US-128`'s ticket-detail screen, whose `AC-61` covers the guarded assign action
and the history timeline. One frontend story closes three backend features' UI.

## Why sprint number ≠ slice number

The eight slices were once mapped one-to-one onto eight sprints; a review found three defects the
mapping itself caused:

1. **Sprint 1 was 215 points across 11 epics** against a two-to-three day budget.
2. **Three stories were scheduled before work they depended on** (delete guard before tickets;
   history before assignment events; customer screen before attachments).
3. **The SLA slice could not measure what it promised** — response-time attainment needs the message
   record, then three slices later (`G-3`).

So slices keep their identity and sprints are sequenced around dependencies. Splitting S1 across five
coherent increments makes all three defects structurally impossible.

## Slice S1 — the assessment deliverable

| Sprint | Name | Features | Stories | Points | Status |
|---|---|---|---|---|---|
| 1 | [Foundation and authentication](#sprint-1--foundation-and-authentication) | `FEAT-01`, `FEAT-02` | 16 | 65 | 15 superseded · 1 done |
| 2 | [Customers, ticket capture and queue](#sprint-2--customers-ticket-capture-and-queue) | `FEAT-03`–`FEAT-05` | 11 | 53 | 11 not started |
| 3 | [Ticket detail, lifecycle, assignment and history](#sprint-3--ticket-detail-lifecycle-assignment-and-history) | `FEAT-06`–`FEAT-08` | 10 | 49 | 10 not started |
| 4 | [Contract hardening, localisation and the journey](#sprint-4--contract-hardening-localisation-and-the-journey) | `FEAT-09`–`FEAT-11` | 5 | 20 | 4 not started · 1 superseded |
| 5 | [Notes and attachments](#sprint-5--notes-and-attachments) | `FEAT-12`, `FEAT-13` | 7 | 29 | 7 not started |

**Amended 2026-08-25:** sprints 1–4 were the defensible core when the backend was being built here.
With the platform adopted, the defensible core is now **the ticket workflow** — sprints 2 and 3 —
because everything else either arrived with the platform or is documentation.
**Sprint 5 is cut first**, attachments before notes. Any cut is recorded in
[`../assessment/rubric-traceability.md`](../assessment/rubric-traceability.md) under **Scope cuts**.

S1 totals 216 points against a two-to-three day budget (constraint `CON-1`). That mismatch is made
legible rather than resolved: running out of time removes a whole sprint — and now a whole set of
shipped features — cleanly, rather than leaving a backend with no screens attached.

---

## Sprint 1 — Foundation and authentication

> The response contract, the bilingual message catalogue, the domain base types, and authentication
> that a person can actually use — sign-in API and sign-in screen in the same sprint.

### FEAT-01 — Platform foundation

**API-only · 11 stories · 42 points · sprint 1**

Enabler. It delivers nothing a support agent would recognise and is sequenced first because nothing
else can be built without it.

Order: US-101 → US-102 → US-103 → US-104 → US-105 → US-106 → US-107 → US-108 → US-109 → US-110 →
US-111.

Status: **all 11 superseded.** These stories described the hand-built envelope, message catalogue,
validation pipeline and domain base types. The adopted platform provides its own equivalents, so the
requirements are largely met in spirit while **none of these criteria is proven against the shipped
code**. Each story says so in its own file rather than carrying a stale `done`.

### FEAT-02 — Authentication and session

**Vertical · 5 stories · 23 points · sprint 1**

| Layer | Stories |
|---|---|
| Backend | US-112 → US-113 → US-114 → US-115 |
| Frontend | US-125 |

Ship gate: sign-in works end to end in a browser — valid credentials reach the ticket list, invalid
ones show an error without navigating, and a protected route with no session redirects. Backend
tests plus component tests green.

`US-111`'s `FND-31` — the documentation UI executing an authenticated request — becomes provable once
this feature lands, so revisit it before closing the sprint.

---

## Sprint 2 — Customers, ticket capture and queue

> An agent can record who contacted us, raise a tracked request against them through a validated
> form, and work the resulting queue.

### FEAT-03 — Customer records

**API-only · 5 stories · 21 points · sprint 2**

Order: US-001 → US-116 → US-004 → US-002 → US-117.

Ordering note: `US-117`'s first criterion needs a ticket to exist, so `FEAT-04` lands inside this
sprint too. Co-sprinting resolves what would otherwise be a backwards dependency.

No frontend counterpart — see the `G-5` note above. Customers reach the screen through `FEAT-04`'s
picker and `FEAT-12`/`FEAT-13`'s customer detail.

### FEAT-04 — Ticket capture

**Vertical · 2 stories · 16 points · sprint 2**

| Layer | Stories |
|---|---|
| Backend | US-009 |
| Frontend | US-127 |

Ship gate: an agent creates a ticket through the form, client validation mirrors the server's rules,
and server `errors[]` entries land on the control named by their `field` rather than in a banner.
This is the feature that proves the envelope's validation contract is actually consumable — the
single most valuable early integration check in the slice.

### FEAT-05 — Ticket queue

**Vertical · 4 stories · 16 points · sprint 2**

| Layer | Stories |
|---|---|
| Backend | US-013 → US-035 |
| Frontend | US-038, US-126 |

Ship gate: a paged, filtered list with a working "my tickets" toggle, and loading, empty and error
states that are visually distinct. `US-126` ships here because this is the first data view that can
demonstrate it.

---

## Sprint 3 — Ticket detail, lifecycle, assignment and history

> A ticket moves only along its defined lifecycle, only the right person can move it, every move is
> recorded immutably — and one screen shows all of it.

### FEAT-06 — Ticket detail and lifecycle

**Vertical · 5 stories · 25 points · sprint 3**

| Layer | Stories |
|---|---|
| Backend | US-010 → US-016 → US-118 → US-026 |
| Frontend | US-128 |

Ship gate: the ticket-detail screen shows the customer summary, the history timeline and the status
action; the assign action is hidden for agents **and refused by the server if called anyway**.

`US-128` is the frontend story that also closes `FEAT-07` and `FEAT-08`'s user surface, so it is
sequenced after both — it cannot be finished before the actions and history it renders exist.

### FEAT-07 — Assignment and authorization

**API-only · 3 stories · 16 points · sprint 3**

Order: US-014 → US-119 → US-120.

The security showcase of the slice. Endpoint-level authorization cannot satisfy any of it: only the
handler has loaded the ticket and can see who it is assigned to. UI surface: `US-128`.

### FEAT-08 — Ticket history

**API-only · 2 stories · 8 points · sprint 3**

Order: US-121 → US-022. History follows assignment because assignment events are among what it
records. UI surface: the timeline in `US-128`.

---

## Sprint 4 — Contract hardening, localisation and the journey

> The cross-cutting passes that can only be proven once endpoints and screens exist, then the one
> journey that proves the whole thing persists.

### FEAT-09 — Contract hardening

**API-only · 3 stories · 10 points · sprint 4**

Order: US-122 → US-123 → US-124.

These criteria are continuous obligations, not late work: every feature from `FEAT-02` onward is
expected to satisfy them as it ships. This feature is the pass that **proves** them across the whole
surface, which is only possible once the surface exists.

### FEAT-10 — Localisation

**Frontend-only · 1 story · 5 points · sprint 4**

`US-093`. The server half — both languages in every response — shipped in `FEAT-01`, so what remains
is that no string is hardcoded, direction follows locale, and switching language triggers no refetch.

The mechanism ships here; **reviewed Arabic copy does not**. The catalogue currently holds developer
placeholders (`PA-7`), and sprint 14 is where that is fixed.

### FEAT-11 — End-to-end journey

**Frontend-only · 1 story · 5 points · sprint 4**

`US-129`. Terminal by design: sign in, create a ticket, assign it, change its status, reload, and
confirm the change and its history persisted. It exercises `FEAT-02` through `FEAT-08` in one flow.

**This is the only end-to-end journey in S1** (`AC-64`). Per-feature E2E was considered and rejected:
the spec defines exactly one journey, and adding more would mean amending an approved spec and
exceeding the time budget. Each feature's own gate is served by unit, integration and component
tests.

---

## Sprint 5 — Notes and attachments

> Customer context that is not a ticket. Cut first if time runs out.

### FEAT-12 — Customer notes

**Vertical · 3 stories · 10 points · sprint 5**

| Layer | Stories |
|---|---|
| Backend | US-007 → US-006 |
| Frontend | US-130 |

Ship gate: notes listed newest first on the customer screen and addable through a validated form,
with the author taken from the token and never from the payload.

### FEAT-13 — Customer attachments

**Vertical · 4 stories · 19 points · sprint 5**

| Layer | Stories |
|---|---|
| Backend | US-008 → US-131 → US-132 |
| Frontend | US-133 |

Ship gate: upload within the size limit and content-type allowlist, a hostile filename that cannot
escape the storage directory, and the list on the customer screen. **Cut before `FEAT-12`.**

---

## Roadmap — sequenced by dependency

Each gets its own spec → plan → implement cycle. Stories are allocated; status reflects
whether the stories exist, not whether they are implemented.

| Sprint | Name | Slice | FEAT | Stories | Points | Status |
|---|---|---|---|---|---|---|
| 6 | Conversation record | S5 (part) | `FEAT-14` | US-201, US-202 | 8 | backend done · frontend partial (component tests owed) |
| 7 | Organisation structure | S8 (part) | `FEAT-16` | US-301–US-310 | 38 | shipped except US-306 (blocked, `OQ-5`) and US-310 |
| 8 | SLA and automation | S2 | `FEAT-17` | US-210–US-225 | 56 | policy CRUD+targets+breach+pause-resume+escalation+admin screen shipped, incl. frontend SLA countdown/escalation badge/policy edit (US-222/223/224, 2026-08-27); US-221 already covered by FEAT-07; auto-assignment, business-hours calendar, pre-breach warning and notifications explicitly cut |
| 9 | Notification gateway and communication channels | S5 | `FEAT-15` | US-201–US-205, US-219, OTP verification | 21 | canonical notification-gateway and OTP specs/plans added 2026-08-27; Email and SMS use configured integration URLs; **partial — ticket-created in-app notification shipped 2026-08-28** (bespoke plan `docs/superpowers/plans/EPIC-02-US-016-ticket-created-notification/`): domain-event dispatch after commit → `TICKET_CREATED` InApp notification to the linked customer's user, pushed live over SignalR (AC-N1..AC-N6, incl. live AC-N3 Node client verification). OTP/remaining channels **not implemented** |
| 10 | Customer portal | S3 | `FEAT-22` | US-401–US-415 | 55 | portal `home` + `signup` slice shipped 2026-08-27 (public landing, register→sign-in→dashboard, phone-on-register contract, `/app` guarding; `docs/superpowers/plans/EPIC-07-US-404-portal-home-and-signup/`) · remaining portal stories (US-402–US-415) not yet wired |
| 11 | Knowledge base | S4 | `FEAT-18` | US-501–US-513 | 48 | spec + plan written 2026-08-27; backend schema fully implemented (ContentCategory taxonomy, ContentVersion, ContentView, ContentVote, ContentTicketLink, Publish/Archive/SetFaq/LinkToTicket commands, Arabic diacritic search — all via prior sessions' migrations + handlers). 2026-08-29: public `GET /api/knowledge-base/categories` added (tree of active ContentCategories); public `GET /api/knowledge-base/articles?categoryId=` wired to `GetContentsQuery.CategoryId`; `ContentCategorySeeder` seeds 5 root + ~12 sub categories idempotently on internal host start; frontend portal KB list rewired to `ContentsApi.categories()` + `categoryId` filter, real category grid (click to filter), active-breadcrumb + clear, FAQ bento hidden during category filter, anonymous vote replaced with sign-in CTA; portal KB detail now shows "Sign in to vote" for unauthenticated visitors. Remaining: admin KB screens (create/edit/publish/archive articles), version-diff UI, article-ticket linking admin UX, full-text search ranking |
| 12 | Administration | S9 | `FEAT-19` | US-801–US-805 | 16 | US-801/802/803 shipped (audit log query+viewer, settings UI, plus a fix making AuditLog actually populate). 2026-08-29: users list enriched — the admin users screen now pages and filters by status/role, searches, sorts, creates, toggles active, exports CSV (frontend `staff.api.list` sends `page`/`pageSize`/`sortBy`/`sortDirection`/`search`/`isActive`/`role`); backend `GET /api/Users` gained a `role` query filter (integration test `MVP02_UsersList_CanBeNarrowedToOneRole`, run green x2); permissions matrix screen polished (role/permission count header, description tooltips). US-804/805 are actually implemented — permission matrix endpoints (list/assign/revoke) and the admin matrix UI exist and are exercised; still missing are dynamic authorization policy from assigned permissions and a role-editor UI with permission assignment |
| 13 | Reporting | S6 | `FEAT-20` | US-601–US-610 | 38 | US-601/602/603/604/605 implemented; US-608 adapted — no `Manager` role or populated department columns exist, so it ships as Admin/Supervisor-only gating with no department filter; US-606/607/610 (frontend) shipped 2026-08-29 — side-bar reachability for all five report screens, a `/reports/overview` hub (ticket volume, SLA breach rate, average resolution, CSAT, volume trend, agent leaderboard — all read from the report endpoints), live queue, report screens per US-605/606/607. Dashboard's fabricated CSAT `4.8` and trend chips removed 2026-08-29: the CSAT tile now reads `GET /api/reports/csat` (supervisor-gated, 30-day window, dash when no responses). US-609 (export) cut, no CSV/Excel dependency |
| 14 | Localisation and branding | S8 (rest) | `FEAT-23` (cont.) | US-311–US-314 | 14 | stories defined |
| 15 | AI assist | S7 | `FEAT-21` | US-701–US-708 | 32 | stories defined · **gated on legal decision** |

**FEAT assignments are provisional.** Each sprint will receive its own spec → plan cycle; the
FEAT numbers above are the best-fit mapping from the story files and may be renumbered when
the spec for that sprint is written.

**Corrected 2026-08-27 — three `FEAT-nn` collisions found and fixed.** Row 11 (Knowledge base)
carried `FEAT-11`, already taken by S1's `FEAT-11` (end-to-end journey) — reassigned to `FEAT-18`,
the first number after `FEAT-17` nothing else uses. Row 10 (Customer portal) carried `FEAT-17`,
colliding with row 8's SLA feature — reassigned to `FEAT-15`, which was sitting unused (row 14 had
mistakenly cited `FEAT-15 (cont.)` for localisation, a typo rather than a real feature, fixed in
the same pass back to `FEAT-10 (cont.)` — localisation's actual number from S1). No plan folder or
spec references the old numbers by their `FEAT-nn` tag alone (they're addressed by date+topic), so
nothing downstream needed updating besides this table.

**Corrected 2026-08-27 (plan-corpus resequencing) — collisions resolved definitively.** The
2026-08-27 pass above left Customer portal on `FEAT-15` and Localisation on `FEAT-10 (cont.)`. To
remove any residual ambiguity and leave a clean monotonic map, both were reassigned to the next free
numbers after `FEAT-21`: Customer portal → `FEAT-22`, Localisation and branding → `FEAT-23`.
Knowledge base remains `FEAT-18`. This is a pure renumber of roadmap row labels; no plan folder or
spec is renamed (cross-references use date+topic, not `FEAT-nn`), and no implemented code is
affected. Recorded here rather than silently applied.

**Also corrected in the same pass — `FEAT-14` was duplicated.** Row 6 (Conversation record) and row 9
(Email channel) both carried `FEAT-14`; Email channel is a continuation of the same conversation
work but is a distinct roadmap row, so it was reassigned to `FEAT-15`, which became free when
Customer portal moved to `FEAT-22`. No plan folder or spec is renamed.

Blocked-on references are registered in
[`../product/05-assumptions-and-open-questions.md`](../product/05-assumptions-and-open-questions.md).

Deferred indefinitely (BRD §6.3, each with a stated reason): ERP connectors, the AI chatbot, native
mobile apps.

**Reopened 2026-08-27 — WhatsApp, live chat and web forms.** At explicit request, these three (plus
inbound SMS conversations, previously outbound-notification-only via `FEAT-15`) moved from "deferred
indefinitely" to spec + backend plan. The stated deferral reasons (paid WhatsApp provider, verified
business identity, live-chat staffing) are **not resolved** — they are carried forward as open
questions in the spec's `A11` — so this is planning ahead of the business decision, not a claim the
channels are ready to ship. Canonical spec:
[`EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md`](../superpowers/specs/EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md);
plan: [`EPIC-03-US-201-feat-24-communication-channels/`](../superpowers/plans/EPIC-03-US-201-feat-24-communication-channels/).
**Not implemented** — no code, no migration, no test.

| Sprint | Name | Slice | FEAT | Stories | Points | Status |
|---|---|---|---|---|---|---|
| 16 | Communication channels — WhatsApp, SMS conversations, live chat, web forms | S5 (reopened) | `FEAT-24`–`FEAT-27` | US-230–US-240 (to be filed alongside the spec) | — | spec + backend plan + tasks written 2026-08-27; **not implemented**; blocked on the business decisions in the spec's `A11` (`OQ-CC-1..3`) before any production deployment |

---

## Phase 2 — BI & workflow ("the workflow this system is meant to have")

**Added 2026-08-28.** New epic [EPIC-14](./epics/EPIC-14-phase2-bi-and-workflow.md): the
user-pasted Workflow & BI specification encoded as real-life logic (8-state lifecycle, assignment
gate on work states, SLA pause on both waiting states, escalation as a marker with a named owner,
VIP/complaint rules), a BI layer answering its KPI catalogue, and a UX redesign so the screens "feel
real, not dead HTML". Domain enrichment precedes the lifecycle (Team entity, lifecycle timestamps,
org-chain wiring, escalation owner). **EPIC-13 (mockup fidelity) folds into `FEAT-30`'s presentation
slice** so the visual pass happens once.

| Sprint | Name | Slice | FEAT | Stories | Points | Status |
|---|---|---|---|---|---|---|
| 17 | Phase 2 — workflow state machine + domain enrichment | EPIC-14 s0–s1 | `FEAT-28` | US-901–US-907, US-919 | 29 | stories filed 2026-08-28 · spec approved · plan pending · **not implemented** |
| 18 | Phase 2 — BI executive dashboard | EPIC-14 s2 | `FEAT-29` | US-908–US-911 | 15 | stories filed 2026-08-28 · spec approved · plan pending · **not implemented** |
| 19 | Phase 2 — UX redesign | EPIC-14 s3–s4 | `FEAT-30` | US-912–US-918, US-920–US-921 | 33 | stories filed 2026-08-28 · spec approved · plan pending · **not implemented** |

Sprint 17 pairs backend (lifecycle machine) with the shared frontend status model (`US-919`) shipped
in the same feature; sprints 18–19 follow the same FEAT-ships-vertically rule from the top of this
file. `FEAT-28` is a large production pass and is deliberately split into two delivery slices
(enrichment, then the 8-state machine) — the plan's tasks will be ordered accordingly and each slice
names the `AC-n` its tests cite.
