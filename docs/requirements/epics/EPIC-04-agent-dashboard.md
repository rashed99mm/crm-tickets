# EPIC-04 · Agent dashboard

| | |
|---|---|
| **Epic** | `EPIC-04` |
| **Priority** | P0 |
| **Stories** | 7 specified · 216-point plan share: 34 pts |
| **Sprints** | 2 (queue visibility) · 4 (agent application) |

## Goal

Provide agents with a single workspace for managing their support responsibilities *(rule
specification §8)* — realized in S1 as the agent application: sign-in that lands on work, a usable
paged queue with filters and a "my tickets" toggle, states that never render failure as emptiness,
a form that agrees with the server, a detail screen whose actions match permissions, instant
language switching, and one end-to-end journey proving persistence.

## Why this epic exists

The screen an agent stares at all day. Its non-obvious requirements are all refusals or
distinctions: "no navigation occurs" on a failed sign-in (navigate-then-bounce reads as a bug);
loading, empty and error states visually distinct — because catching an error into an empty array,
this codebase's default idiom, renders a server outage as "no tickets"; submit disabled while
invalid **and while in flight** — how one impatient double-click becomes two tickets; the assign
action hidden for agents *and refused by the server if called anyway* — hiding is usability, the
refusal is the security control.

## Stories

| Story | Title | Priority | Points | Status | Criteria |
|---|---|---|---|---|---|
| [US-035](../user-stories/US-035-agent-sees-own-work.md) | An agent sees only their own work *(rule proposal: View Assigned Tickets)* | P0 | 3 | `not started` | AC-34 |
| [US-038](../user-stories/US-038-usable-ticket-list.md) | The ticket list is usable *(rule proposal: Filter and Sort Ticket Workload)* | P0 | 5 | `not started` | AC-57 |
| [US-125](../user-stories/US-125-sign-in-and-land-on-work.md) | Sign in and land on the work | P0 | 5 | `not started` | AC-55, AC-56 |
| [US-126](../user-stories/US-126-empty-never-looks-like-failure.md) | An empty list never looks like a failure | P0 | 3 | `not started` | AC-58 |
| [US-127](../user-stories/US-127-validated-create-ticket-form.md) | Create a ticket through a form that agrees with the server | P0 | 8 | `not started` | AC-59, AC-60 |
| [US-128](../user-stories/US-128-ticket-detail-with-guarded-actions.md) | Ticket detail shows the story and hides what I cannot do | P0 | 5 | `not started` | AC-61 |
| [US-129](../user-stories/US-129-end-to-end-journey.md) | One journey proves the whole flow persists | P1 | 5 | `not started` | AC-64 |

Absorbs former epic `EP-1.09` Agent application. The bilingual mechanism ships with US-093
(EPIC-12); reviewed Arabic copy does not — `PA-7`, sprint 14.

## Reserved backlog (unspecified — titles only, no fabricated rules)

| Rule proposal | Future home | Blocked on |
|---|---|---|
| US-036 View Unassigned Tickets | unsupplied in S1 spec (supervisor queue filter covers it partially via US-013) | — |
| US-037 View Customer Context | customer summary travels with ticket (US-010); richer context panel unspecified | — |
| US-039 Create Task · US-040 Manage Reminder | slice S2 per `PA-4` — itself a proposal, `G-1` | `G-1` decision |
| US-041 Use Quick Reply | S5 per `PA-4` (`G-1`) | `G-1`, message record (sprint 6) |
| US-042 Collaborate with Team Members | S5 per `PA-4` (`G-1`) | `G-1` |
| US-043 View SLA Risk | S2 dashboard | `OQ-2`, `OQ-3`, `DEP-3` |
