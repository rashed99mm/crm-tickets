# Customer Support CRM — MVP requirements

**Date:** 2026-08-26
**Replaces:** `docs/requirements/epics/` and `docs/requirements/user-stories/` as the *forward*
backlog. The old set is **kept, not deleted** — it is the record of what was built and how, and 24 of
its stories are shipped and tested. Nothing here rewrites history; it changes what happens next.

**Source:** the brief in [`../../assessment/brief.md`](../../assessment/brief.md), verbatim, all
twelve areas.

---

## Why the previous backlog was replaced

49 story files, 45 of them carrying an estimate, a status row, a test-case table and a traceability
entry. Three specific defects, each of which cost real days:

### 1. Fifteen of the stories were never stories

`US-101` *Uniform response envelope* · `US-102` *One place decides the HTTP status* · `US-104`
*Validation failures arrive keyed to their field* · `US-105` *The validation pipeline runs without
reflection* · `US-108` *Domain base types with identity and component equality* · `US-110` *The
dependency rule is enforced by the build* · `US-111` *The API documents itself truthfully* — and
eight more.

These are architectural constraints. **No user wants "domain base types with component equality".**
None of them can be demonstrated to a support manager. They were given `As a…` sentences, points,
acceptance criteria, test-case tables and status rows — and then the platform adoption made all
fifteen `superseded` in one stroke. That is **45 points of pure bookkeeping, now dead**.

They are replaced by [`definition-of-done.md`](./definition-of-done.md): one charter, applied to
every story, maintained in one place.

### 2. Ten more were acceptance criteria wearing a story's clothes

`US-116` *a duplicate email is a conflict* is a criterion of "record a customer". `US-118` *every
other transition is refused* and `US-120` *status changes belong to the assignee* are criteria of
"move a ticket along its lifecycle". `US-119` *an agent cannot assign* is a criterion of "a
supervisor assigns work". `US-126` *an empty list never looks like a failure* is a criterion of
"work the queue".

Promoting a criterion to a story does not make it more likely to be built. It triples the artifacts
for one behaviour and makes "done" ambiguous — the parent looks finished while a criterion of it sits
unstarted somewhere else in the folder.

### 3. Every capability was split across two stories, then reconnected by hand

**All 49 stories carry a `Ships with` row.** `US-009` *raise a ticket* (Backend) and `US-127` *create
a ticket through a form* (Frontend) are one capability. So are `US-013`/`US-038`, `US-010`/`US-128`,
`US-007`/`US-130`, `US-008`/`US-133`.

The delivery plan then needed a rule — *"a backend story is not done until its counterpart is
done"* — to undo a split that should never have happened. **A story is a vertical slice by
definition.** If a `Ships with` row is required to reconnect two halves, the split was wrong. That
ratio, 40 backend stories to 9 frontend, is the symptom.

---

## What is actually built — the part worth knowing

The premise that "we haven't finished one module" is **not accurate**, and the old backlog is why it
looked that way. Against the brief's twelve areas:

| # | Brief area | State |
|---|---|---|
| **2** | **Ticket Management** | **Complete** — create, categories, priorities, assign, status machine, history. `AC-29`…`AC-50` all tested |
| **10** | **Security & Administration** | **Substantially complete** — users, roles, permissions, audit trail, configuration |
| **1** | Customer Management | **~70%** — profiles, contact details, search, delete guard done. **Interaction history and attachments not built** |
| 4 | Agent Dashboard | Partial — "my tickets" filter exists; no dashboard screen |
| 6 | Knowledge Base | Arrived with the platform (`Contents`), unverified against a story |
| 11 | Integrations | Arrived with the platform (`ExternalApiConfigurations`) |
| 12 | Platform | Bilingual **backend** done; the **UI is English-only** |
| 3, 5, 7, 8, 9 | Channels, SLA, AI, Portal, Reports | Not started |

242 backend tests and 104 frontend tests pass. Two modules are finished. The backlog simply could not
show it.

---

## The MVP boundary

**In.** A support team can work on day one: sign in, record who called, log what they asked for,
work the queue, move it to done, hand it to the right person, and read the whole interaction later —
in their own language.

Areas **1, 2, 4 (thin), 10 (thin), 12 (language only)**.

**Out, with the reason** — each of these is a project, not a story:

| Area | Why it is out |
|---|---|
| 3 — Channels | Five channels, each needing a provider, credentials and an inbound webhook. Email alone is a week |
| 5 — SLA & automation | Response-time attainment cannot be measured until a message record exists. Building targets first means building something unmeasurable |
| 7 — AI | The unresolved question is legal, not technical: what customer data may leave the tenant |
| 8 — Customer portal | Needs a second identity store — customers are not staff — and a public trust boundary |
| 9 — Reports | Nothing worth reporting until SLA data and real volume exist |
| 6 — Knowledge base | The platform already ships it. Re-specifying it now is work with no new outcome |
| 11 — Integrations | The configuration surface already ships |
| 12 — multi-branch, branding | Organisation structure is not modelled, and nothing in the MVP needs it |

**Cut order if time runs short:** `MVP-13` (bilingual UI) → `MVP-12` (dashboard) → `MVP-06`
(attachments). Never cut `MVP-05` (notes): "interaction history" is named in brief area 1 and a
support CRM that cannot record what was said is not a support CRM.

---

## The backlog

Five epics, thirteen stories, **one file per epic**. Stories live inside their epic rather than in
their own file — 49 files with status rows to maintain is the overhead this replaces.

| Epic | Stories | Remaining |
|---|---|---|
| [1 — Staff access](./epic-1-staff-access.md) | `MVP-01`, `MVP-02` | 0 — **complete 2026-08-26** |
| [2 — Customer records](./epic-2-customer-records.md) | `MVP-03`…`MVP-06` | 0 — **complete 2026-08-26** |
| [3 — Ticket workflow](./epic-3-ticket-workflow.md) | `MVP-07`…`MVP-11` | 0 |
| [4 — Agent workspace](./epic-4-agent-workspace.md) | `MVP-12` | 0 — **complete** |
| [5 — Bilingual platform](./epic-5-bilingual-platform.md) | `MVP-13` | 0 — **complete** |

**All thirteen are shipped end to end.**

**Brief area 1 (Customer Management) is complete as of 2026-08-26** — profiles, contact details,
interaction history and attachments, screens included. That closes the old `G-5` gap, which had been
recorded and left open since Phase 2.

**The MVP is delivered.** 13 of 13 stories `done`. `MVP-02`'s last gap — a screen-level test proving
a non-admin is not offered the staff screen and the route guard redirects to `/forbidden` — closed
2026-08-26 in `app.routes.spec.ts`.

Brief areas **1, 2, 4 (thin), 10 (thin) and 12 (language)** are complete.

## Rules these stories follow

1. **A story is vertical.** UI, API, data and tests. There is no `Layer` field and no `Ships with`
   row, because there is nothing to reconnect.
2. **A story is demoable.** If it cannot be shown to a support manager, it is not a story — it is a
   task, a criterion, or an NFR.
3. **Criteria live inside their story.** Nothing gets promoted.
4. **Quality attributes live in the charter.** [`definition-of-done.md`](./definition-of-done.md)
   applies to every story; it is not restated per story and not counted in points.
5. **A story is one day or less, full-stack.** Anything larger is an epic that has not been split.
