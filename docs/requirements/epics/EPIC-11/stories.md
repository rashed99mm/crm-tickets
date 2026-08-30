# EPIC-11 · AI Features — Stories (All Slices)

| Epic | Slice(s) | BRD Requirements |
|---|---|---|
| `EPIC-11` | S7, Deferred | FR-7.1–FR-7.7 |

> **Gating rule (B5, PA-9):** No personal data reaches an external model provider without a
> recorded data-processing decision. S7 is gated on that legal decision, which is not technical.

---

## S7 — AI Provider Integration

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-701 | AI provider configuration (OpenAI / Azure) | Backend | S7 | M | `not started` | DEP-6, OQ-8 |
| US-702 | AI service port (interface in Application, impl in Infrastructure) | Backend | S7 | M | `not started` | FR-7.1–FR-7.4 |
| US-703 | Human confirmation gate (BR-19: no auto-send, no auto-state-change) | Backend | S7 | M | `not started` | FR-7.6, BR-19 |

---

## S7 — Agent Assistance

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-704 | Summarise long ticket thread | Backend | S7 | S | `not started` | FR-7.1 |
| US-705 | Suggest category + priority at creation (overridable) | Backend | S7 | S | `not started` | FR-7.2 |
| US-706 | Draft suggested reply from ticket context + KB | Backend | S7 | S | `not started` | FR-7.3 |
| US-707 | Suggest KB solutions for a ticket | Backend | S7 | S | `not started` | FR-7.4 |

---

## S7 — Suggestion Tracking

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-708 | Suggestion entity (Type, Content, Status: accepted/edited/rejected) | Backend | S7 | M | `not started` | FR-7.5 |
| US-708 | Record suggestion outcome endpoint | Backend | S7 | M | `not started` | FR-7.5 |
| — | Suggestion display + action UI on ticket detail | Frontend | S7 | M | `not started` | FR-7.5 |

---

## S7 — Frontend

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| — | AI summary panel on ticket detail | Frontend | S7 | S | `not started` | FR-7.1 |
| — | Category/priority suggestion on create form | Frontend | S7 | S | `not started` | FR-7.2 |
| — | Suggested reply panel with accept/edit/reject | Frontend | S7 | S | `not started` | FR-7.3 |
| — | KB solution suggestions on ticket detail | Frontend | S7 | S | `not started` | FR-7.4 |

---

## Deferred (BRD §6.3)

| Story | Title | Status | Reason |
|---|---|---|---|
| FR-7.7 | Customer-facing AI chatbot | Deferred | Misrepresented if stubbed; worse than none if wrong |

---

## Summary

| Category | Total Stories | Done | Not Started |
|---|---|---|---|
| Provider Integration | 3 | 0 | 3 |
| Agent Assistance | 4 | 0 | 4 |
| Suggestion Tracking | 3 | 0 | 3 |
| Frontend | 4 | 0 | 4 |
| Deferred | 1 | — | — |
| **Total** | **14** | **0** | **14** |
