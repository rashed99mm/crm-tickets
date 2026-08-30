# Epic 4 — Agent workspace

**Brief area:** 4 (Agent Dashboard) — assigned tickets, customer information, tasks and reminders,
quick replies, team collaboration

**MVP scope: one story, and four of the five bullets are out.** This is the epic where scope creep
is most tempting, so the boundary is drawn explicitly:

| Bullet | In MVP? | Why |
|---|---|---|
| Assigned tickets | **Yes** — `MVP-12` | The agent's own work is the point of a dashboard |
| Customer information | Already shipped | The ticket detail screen carries the customer summary |
| Tasks and reminders | **No** | A second work-item type with its own lifecycle and notifications. That is a module, not a dashboard widget |
| Quick replies | **No** | Requires a reply mechanism, which requires a communication channel. Area 3 is out |
| Team collaboration | **No** | Internal mentions and threads need a notification surface and a second read model |

---

## `MVP-12` — My work at a glance · **NOT BUILT**

**As an** agent, **I want** one screen that shows what is on my plate, **so that** I know what to do
next without filtering the queue every morning.

**Status:** `not started`. The data exists — `GET /api/Tickets?mine=true` ships and is tested. What
does not exist is a screen that answers "what should I do next".

### Acceptance criteria

1. Given I sign in, my dashboard shows **my open tickets**, newest first, without me setting a
   filter.
2. Given my tickets, I see a count by status — how many are `New`, `Open`, `Pending` — so I can see
   the shape of my workload, not just a list.
3. Given a ticket on the dashboard, clicking it opens the ticket detail.
4. Given I have nothing assigned, the dashboard says so plainly and **does not look like a failure**.
5. Given the request fails, I see an error with a retry — visibly different from having no work.
6. Given I am a supervisor, I additionally see how many tickets are **unassigned**, because
   distributing work is my job.

### Notes

Criterion 2 is what separates a dashboard from a filtered list. A list of twelve tickets does not
tell an agent whether they are behind; "4 New, 6 Open, 2 Pending" does.

Criterion 6 is the only supervisor-specific element, and it is deliberately a **count, not a second
queue** — clicking through to the existing queue with a filter is the whole feature.

**No new backend endpoint should be needed.** `GET /api/Tickets?mine=true` returns a paged envelope
carrying `totalCount`, and per-status counts can come from the same endpoint with a status filter.
If the counts turn out to need a purpose-built read, that is a finding to record — not an assumption
to build on.

### Out of scope for this story, stated so it is not read as an omission

No charts. No date-range selector. No "team view". No refresh timer. Those belong to Reports
(area 9), which is out of the MVP.
