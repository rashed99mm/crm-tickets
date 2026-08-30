# EPIC-07 · Customer Portal — Stories (All Slices)

| Epic | Slice(s) | BRD Requirements |
|---|---|---|
| `EPIC-07` | S3 | FR-8.1–FR-8.9 |

---

## S3 — Customer Authentication

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-401 | Customer registration (create Customer + portal credentials) | Backend | S3 | M | `not started` | FR-8.1 |
| US-402 | Customer login endpoint (separate from staff JWT) | Backend | S3 | M | `not started` | FR-8.1 |
| US-403 | Customer-scoped authorization (own records only, BR-20) | Backend | S3 | M | `not started` | FR-8.9, BR-20 |
| US-410 | Portal login screen | Frontend | S3 | M | `not started` | FR-8.1 |
| — | Portal auth interceptor + session store | Frontend | S3 | M | `not started` | FR-8.1 |
| — | Portal auth guard (redirect to login) | Frontend | S3 | M | `not started` | FR-8.9 |

---

## S3 — Ticket Submission

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-404 | Customer submits ticket through portal form | Backend | S3 | M | `not started` | FR-8.2 |
| US-411 | Portal ticket submission form (subject, description, category) | Frontend | S3 | M | `not started` | FR-8.2 |

---

## S3 — Ticket Tracking

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-405 | Customer's own tickets list (scoped to customer) | Backend | S3 | M | `not started` | FR-8.3, FR-8.4 |
| US-406 | Ticket status + reference display | Backend | S3 | M | `not started` | FR-8.3 |
| US-412 | Portal: my tickets list | Frontend | S3 | M | `not started` | FR-8.3, FR-8.4 |
| US-413 | Portal: ticket detail (status, reference, history) | Frontend | S3 | M | `not started` | FR-8.3, FR-8.4 |

---

## S3 — Customer Replies

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-407 | Customer replies to agent on open request | Backend | S3 | M | `not started` | FR-8.5 |
| US-414 | Portal: reply form on ticket detail | Frontend | S3 | M | `not started` | FR-8.5 |

---

## S3 — Knowledge Base in Portal

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| — | Browse published articles (backend exists) | Backend | S3 | S | `done` | FR-8.6 |
| — | Portal: browse + search KB articles | Frontend | S3 | S | `not started` | FR-8.6 |

---

## S3 — Satisfaction Survey

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-408 | `SurveyResponse` entity (TicketId, Rating 1-5, FreeText?) | Backend | S3 | M | `not started` | FR-8.7, FR-8.8 |
| US-409 | Survey endpoint (submit rating + feedback) | Backend | S3 | M | `not started` | FR-8.7, FR-8.8 |
| — | Survey invitation on ticket resolution | Backend | S3 | M | `not started` | FR-8.7 |
| US-415 | Portal: satisfaction survey form | Frontend | S3 | M | `not started` | FR-8.7, FR-8.8 |

---

## Summary

| Category | Total Stories | Done | Not Started |
|---|---|---|---|
| Authentication | 6 | 0 | 6 |
| Ticket Submission | 2 | 0 | 2 |
| Ticket Tracking | 4 | 0 | 4 |
| Customer Replies | 2 | 0 | 2 |
| Knowledge Base | 2 | 1 | 1 |
| Satisfaction Survey | 4 | 0 | 4 |
| **Total** | **20** | **1** | **19** |
