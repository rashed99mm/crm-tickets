# EPIC-02 · Ticket Management — Stories (All Slices)

| Epic | Slice(s) | BRD Requirements |
|---|---|---|
| `EPIC-02` | S1, S2, S3, S5, S9 | FR-2.1–FR-2.16 |

---

## S1 — Ticket Lifecycle (Core)

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-009 | Raise a ticket for a customer (category + priority + reference) | Backend | S1 | P0 | `done` | FR-2.1, FR-2.2, FR-2.3, BR-14, BR-15 |
| US-013 | Filter the queue (status, priority, assignee, combine) | Backend | S1 | P0 | `done` | FR-2.4, FR-2.5 |
| US-035 | Agent sees only their own assigned tickets | Backend | S1 | P0 | `done` | FR-2.6 |
| US-010 | Ticket detail with customer summary + history | Backend | S1 | P0 | `done` | FR-2.7 |
| US-016 | Move a ticket along the lifecycle | Backend | S1 | P0 | `done` | FR-2.8 |
| US-118 | Refuse undefined transitions (state conflict, not validation) | Backend | S1 | P0 | `done` | FR-2.8, BR-3, BR-4 |
| US-026 | Reopen a resolved/closed ticket + optimistic concurrency | Backend | S1 | P1 | `done` | FR-2.10, FR-2.11, BR-13, BR-18 |
| US-120 | Status change belongs to the ticket's assignee | Backend | S1 | P0 | `done` | FR-2.8, BR-11 |
| US-014 | Supervisor assigns/reassigns a ticket | Backend | S1 | P0 | `done` | FR-2.12, BR-10 |
| US-119 | Agent cannot assign (server refuses, not just hidden) | Backend | S1 | P0 | `done` | FR-2.12, AC-43 |
| US-121 | Every change recorded immutably (append-only history) | Backend | S1 | P0 | `done` | FR-2.13, BR-5 |
| US-127 | Validated create ticket form (client mirrors server rules) | Frontend | S1 | P0 | `done` | FR-2.1, FR-2.3 |
| US-038 | Usable ticket list (paged, filtered) | Frontend | S1 | P0 | `done` | FR-2.4, FR-2.5, AC-57 |
| US-126 | Empty state never looks like failure | Frontend | S1 | P0 | `done` | AC-58 |
| US-128 | Ticket detail with guarded actions + history timeline | Frontend | S1 | P0 | `done` | FR-2.7, FR-2.8, FR-4.2, FR-4.4, FR-4.5 |

**S1 gaps:**
- G-8: Assignee name missing from read models (queue + detail show placeholder)
- US-035: Assignee filter may be partial (needs FEAT-07 endpoint — now exists)

---

## S2 — Escalation State

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-225 | Add Escalation State to Ticket Entity | Backend | S2 | P0 | `not started` | FR-2.14 |
| — | Escalation state transitions on SLA breach | Backend | S2 | M | `not started` | FR-2.14 |
| — | Escalation badge on ticket queue + detail | Frontend | S2 | M | `not started` | FR-2.14 |

---

## S3 — Web Form Channel (customer creates ticket)

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-404 | Customer Submits Ticket Through Portal | Backend | S3 | P0 | `not started` | FR-2.15 |
| — | Record channel origin on ticket | Backend | S3 | M | `not started` | FR-2.15 |
| — | Portal ticket submission form | Frontend | S3 | M | `not started` | FR-2.15 |

---

## S5 — Message Record (ticket communication)

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-201 | Record inbound/outbound message against ticket | Backend | S5 | P0 | `not started` | FR-2.15 |
| US-202 | Message timeline on ticket detail | Frontend | S5 | P0 | `not started` | FR-2.15 |

---

## S9 — Category Taxonomy Management

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| — | Maintain category taxonomy without deployment | Backend | S9 | S | `not started` | FR-2.16 |
| — | Category management admin UI | Frontend | S9 | S | `not started` | FR-2.16 |

---

## Summary

| Slice | Total Stories | Done | Not Started |
|---|---|---|---|
| S1 | 15 | 15 | 0 |
| S2 | 3 | 0 | 3 |
| S3 | 3 | 0 | 3 |
| S5 | 2 | 0 | 2 |
| S9 | 2 | 0 | 2 |
| **Total** | **25** | **15** | **10** |
