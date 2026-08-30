# EPIC-02 · Ticket management

| | |
|---|---|
| **Epic** | `EPIC-02` |
| **Priority** | P0 |
| **Stories** | 11 specified · 216-point plan share: 57 pts |
| **Sprints** | 2 (capture, queue) · 3 (lifecycle, assignment, history) |
| **Criteria** | AC-29…AC-50 — see [`../slice-s1-coverage.md`](../slice-s1-coverage.md) |

## Goal

Enable support teams to create, track, assign, update, escalate, and resolve customer support
requests *(rule specification §8)* — realized here as capture with controlled category and
human-readable reference, a queue that filters and combines filters, a status machine that refuses
undefined transitions as state conflicts, supervisory assignment closed to agents, per-record
ownership authorization, and an append-only history.

## Why this epic exists

The lifecycle is the product's spine. Three rules make it trustworthy rather than decorative:
status changes only along the transition table and any other jump is a **409 state conflict**, not a
validation error (`BR-3`, `BR-4`); only a supervisor assigns, including to themselves (`BR-10`); an
agent progresses only their own tickets while a supervisor changes any (`BR-11`). History is
append-only by rule, not by convention (`BR-5`), and concurrent edits refuse the later write rather
than silently overwriting (`BR-13`). Escalation *state* belongs here; escalation *rules* are S2 —
the brief's ambiguity ruling.

## Stories

| Story | Title | Priority | Points | Status | Criteria |
|---|---|---|---|---|---|
| [US-009](../user-stories/US-009-raise-a-ticket.md) | Raise a ticket for a customer's request *(rule proposal: Create Ticket)* | P0 | 8 | `not started` | AC-29…AC-31 |
| [US-010](../user-stories/US-010-ticket-detail.md) | Open a ticket and see its whole story *(rule proposal: View Ticket)* | P0 | 5 | `not started` | AC-35, AC-36 |
| [US-013](../user-stories/US-013-filter-the-queue.md) | Work through the queue with filters *(rule proposals: Filter Tickets · Search Tickets)* | P0 | 5 | `not started` | AC-32, AC-33 |
| [US-014](../user-stories/US-014-supervisor-assigns-work.md) | A supervisor assigns work *(rule proposal: Assign Ticket to Agent)* | P0 | 5 | `not started` | AC-42, AC-44 |
| [US-016](../user-stories/US-016-move-along-the-lifecycle.md) | Move a ticket along its lifecycle *(rule proposal: Change Ticket Status)* | P0 | 5 | `not started` | AC-37 |
| [US-022](../user-stories/US-022-read-ticket-history.md) | Read a ticket's history *(rule proposal: View Ticket History)* | P1 | 3 | `not started` | AC-50 |
| [US-026](../user-stories/US-026-reopen-and-refuse-lost-updates.md) | Reopen a ticket, and never lose a concurrent change *(rule proposal: Reopen Ticket)* | P1 | 5 | `not started` | AC-40, AC-41 |
| [US-118](../user-stories/US-118-refuse-undefined-transitions.md) | Every other transition is refused | P0 | 5 | `not started` | AC-38, AC-39 |
| [US-119](../user-stories/US-119-agent-cannot-assign.md) | An agent cannot assign anything | P0 | 3 | `not started` | AC-43 |
| [US-120](../user-stories/US-120-status-change-belongs-to-assignee.md) | Status changes belong to the ticket's own assignee | P0 | 8 | `not started` | AC-45…AC-47 |
| [US-121](../user-stories/US-121-every-change-recorded-immutably.md) | Every change is recorded and nothing can rewrite it | P0 | 5 | `not started` | AC-48, AC-49 |

Absorbs former epics `EP-1.04` Ticket capture and retrieval, `EP-1.05` Ticket status machine,
`EP-1.06` Assignment and per-record authorization, `EP-1.07` Ticket history and audit.
Ordering note preserved from US-117: its first criterion cannot be proven until US-009 exists —
co-sprinting sprints 2 makes that resolvable rather than a backwards cross-sprint dependency.

## Reserved backlog (unspecified — titles only, no fabricated rules)

| Rule proposal | Future home | Blocked on |
|---|---|---|
| US-011 Update Ticket | later slices (subject/category correction unspecified in S1 spec) | — |
| US-015 Reassign Ticket | folded into US-014's assign operation today; distinct story when workflow needs it | — |
| US-017 Change Ticket Priority | unscheduled | priority change rules (`BR-17` interplay) need S2 agreement |
| US-018 Categorize Ticket | at creation in US-009; re-categorization unscheduled | taxonomy maintenance — `G-2` |
| US-019 Add Customer Reply | S5 message record, sprint 6 | `OQ-11` |
| US-020 Add Internal Note (ticket-level) | unscheduled | — |
| US-021 Add Ticket Attachment | unscheduled (customer attachments ship first) | — |
| US-023 Escalate Ticket | S2, sprint 8 | escalation *rules* are S2 per the brief's ambiguity ruling |
| US-024 Resolve Ticket / US-025 Close Ticket | realized inside the transition table (US-016/US-118) | distinct stories if workflow diverges |
