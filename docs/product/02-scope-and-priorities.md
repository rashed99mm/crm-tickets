# Scope and priorities

Priority owners: the brief's slice decomposition ([`../assessment/brief.md`](../assessment/brief.md))
and the BRD's delivery phasing (§6.2). This file restates them in the rule specification's
P0/P1/P2 module vocabulary so the two cuts can be compared. **Where they disagree, the brief and
BRD win** — the P0/P1/P2 marks below are a reading, not a re-decision.

## Module priorities

| Module | Priority here | Where it lands | Note |
|---|---|---|---|
| Customer Management | **P0** | S1 · sprints 2, 5 | Profiles, contact details, notes, attachments; interaction timeline beyond notes is later |
| Ticket Management | **P0** | S1 · sprints 2–3 | Create/track/assign/status/history; escalation *state* only — rules are S2 |
| Security & Administration | **P0** | S1 · sprint 1 | Authentication, two roles, per-record authorization; remainder proposed as S9 (`G-2`) |
| Agent Dashboard | **P0** | S1 · sprints 4 | Assigned tickets, customer context, validated forms; tasks/reminders/quick replies deferred (`G-1`) |
| SLA & Basic Automation | **P1** | S2 · sprint 8 | Blocked on `OQ-2`, `OQ-3`, `DEP-2`, `DEP-3` |
| Email Communication | **P1** | S5 · sprints 6, 9 | Message record pulled forward to sprint 6 (`G-3`); provider integration at 9 |
| Knowledge Base | **P1** | S4 · sprint 11 | |
| Customer Portal | **P1** | S3 · sprint 10 | |
| Standard Reports & Dashboards | **P1** | S6 · sprint 13 | |
| Arabic & English (full) | **P1** | S8 · sprints 7, 14 | Bilingual message *foundation* is already P0-done in S1 |
| Audit Logs (system-wide) | **P1** | S9 · sprint 12 *(proposed)* | Ticket history audit exists in S1; system log unscheduled (`RSK-8`) |
| Integrations / ERP | **P1** | unscheduled | No named ERP — `OQ-9`, `DEP-7` |
| WhatsApp, Live Chat, SMS, Web Forms | **P1/P2** | deferred indefinitely | Provider + staffing cost (BRD §6.3); web forms reach the portal as S3 |
| AI Features | **P2** | S7 · sprint 15 | Gated on `OQ-8` — legal, not technical (`PA-9`) |
| Advanced branding / multi-branch config | **P2** | S8 · sprint 14 / 7 | Branch scoping early (`RSK-7`), branding late |

## The assessment deliverable

**S1 — ticket lifecycle, sprints 1–5, 49 stories, 216 points.** It covers every rubric criterion on
its own and everything else needs a ticket to exist. Within it:

- **Sprints 1–4 are the defensible core.**
- **Sprint 5 is cut first**, attachments before notes.
- Any cut is recorded in [`../assessment/rubric-traceability.md`](../assessment/rubric-traceability.md)
  under **Scope cuts** — an unexplained gap reads as an oversight, a recorded cut reads as a decision.

The 216-points-against-two-to-three-days mismatch is recorded in the brief (time budget section)
and is made legible, not resolved, by the plan: running out of time removes a whole sprint cleanly.

## Story-level priority

Every story carries its own `P0/P1/P2`. Distribution across S1's 49 stories: 33 × P0, 14 × P1,
2 × P2 — see the epic files under [`../requirements/epics/`](../requirements/epics/) for per-story
marks.
