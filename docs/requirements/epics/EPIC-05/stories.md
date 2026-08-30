# EPIC-05 · SLA & Automation — Stories (All Slices)

| Epic | Slice(s) | BRD Requirements |
|---|---|---|
| `EPIC-05` | S2 | FR-5.1–FR-5.10 |

---

## S2 — SLA Policies & Targets

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-210 | Create `SLAPolicy` entity (Priority, ResponseTarget, ResolutionTarget, CategoryId?, BranchId?) | Backend | S2 | P0 | `not started` | FR-5.1, BR-01 |
| US-211 | Create `SLAEvent` entity (TicketId, TargetType, TargetAt, BreachedAt, PausedSeconds) | Backend | S2 | P0 | `not started` | FR-5.5, BR-15 |
| US-212 | Compute SLA targets on ticket creation from matching SLAPolicy | Backend | S2 | P0 | `not started` | FR-5.2, BR-13 |
| US-214 | `SLAPoliciesController` (CRUD for admin) | Backend | S2 | P0 | `not started` | FR-5.1, BR-01 |

---

## S2 — SLA Clock (Pause/Resume)

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-213 | Pause SLA clock on transition to Pending (BR-16) | Backend | S2 | P1 | `not started` | FR-5.3, BR-16, BR-17 |

---

## S2 — Business Hours Calendar

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-215 | Branch business-hours calendar entity + configuration | Backend | S2 | P1 | `not started` | FR-5.4, BR-14, BR-17 |

---

## S2 — Breach Detection & Escalation

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-216 | Background job: monitor SLA clocks and detect breaches | Backend | S2 | P0 | `not started` | FR-5.5, FR-5.6, BR-18 |
| US-217 | Warn before breach (imminent breach notification) | Backend | S2 | P1 | `not started` | FR-5.9, BR-20 |
| US-218 | Auto-escalate ticket when threshold crossed | Backend | S2 | P0 | `not started` | FR-5.7 |
| US-219 | Notify assignee + supervisor on breach | Backend | S2 | P0 | `not started` | FR-5.8 |
| US-225 | Add `EscalationState` to Ticket entity | Backend | S2 | P0 | `not started` | FR-2.14 |

---

## S2 — Auto-Assignment

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-220 | Auto-assignment rules engine (round-robin / load-based) | Backend | S2 | P0 | `not started` | FR-5.6 |
| US-221 | Supervisor override of auto-assignment (recorded in history) | Backend | S2 | P1 | `not started` | FR-5.10 |

---

## S2 — Frontend

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-222 | SLA countdown indicator on ticket detail | Frontend | S2 | P0 | `not started` | FR-5.2 |
| US-223 | SLA policy management screen (admin) | Frontend | S2 | P0 | `not started` | FR-5.1 |
| US-224 | Escalation badge on ticket queue | Frontend | S2 | P1 | `not started` | FR-5.7 |

---

## Summary

| Category | Total Stories | Done | Not Started |
|---|---|---|---|
| Policies & Targets | 4 | 0 | 4 |
| SLA Clock | 1 | 0 | 1 |
| Business Hours | 1 | 0 | 1 |
| Breach & Escalation | 5 | 0 | 5 |
| Auto-Assignment | 2 | 0 | 2 |
| Frontend | 3 | 0 | 3 |
| **Total** | **16** | **0** | **16** |
