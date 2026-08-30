# EPIC-01 · Customer Management — Stories (All Slices)

| Epic | Slice(s) | BRD Requirements |
|---|---|---|
| `EPIC-01` | S1, S3, S5, S8 | FR-1.1–FR-1.18 |

---

## S1 — Customer CRUD, Notes, Attachments

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-001 | Create a customer with validated details | Backend | S1 | P0 | `done` | FR-1.1, FR-1.2 |
| US-116 | Duplicate email is a conflict, not a validation error | Backend | S1 | P0 | `done` | FR-1.3 |
| US-004 | Find a customer in a long list (search + pagination) | Backend | S1 | P0 | `done` | FR-1.4, FR-1.5 |
| US-002 | Read and correct a customer's details | Backend | S1 | P0 | `done` | FR-1.6, FR-1.7 |
| US-117 | A customer with history cannot be deleted | Backend | S1 | P0 | `done` | FR-1.8, FR-1.9 |
| US-007 | Record a note against a customer (author from token) | Backend | S1 | P1 | `done` | FR-1.11, BR-6 |
| US-006 | Read a customer's notes newest first | Backend | S1 | P1 | `done` | FR-1.12 |
| US-008 | Attach a file within size limit and type allowlist | Backend | S1 | P1 | `done` | FR-1.13, FR-1.14 |
| US-131 | Hostile filename cannot escape the storage directory | Backend | S1 | P1 | `done` | FR-1.14, NFR-7 |
| US-132 | Retrieve and remove an attachment | Backend | S1 | P2 | `done` | FR-1.15, FR-1.16 |
| US-130 | Notes appear on the customer screen | Frontend | S1 | P1 | `done` | FR-1.11, FR-1.12 |
| US-133 | Attachments appear on the customer screen | Frontend | S1 | P2 | `done` | FR-1.13 |
| — | Customer list screen (search + pagination) | Frontend | S1 | P0 | `done` | FR-1.4, FR-1.5 |
| — | Customer create screen | Frontend | S1 | P0 | `done` | FR-1.1, FR-1.2 |
| — | Customer detail screen (profile + notes + attachments) | Frontend | S1 | P0 | `done` | FR-1.6 |

**S1 gaps:**
- G-7: Ticket queue cannot filter by customerId (backend has filter, frontend doesn't use it on customer detail)
- G-8: Assignee name missing from read models (affects customer detail ticket lane)

---

## S3 — Customer Portal (interaction history visible to customer)

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-059 | Customer sees own tickets on portal | Frontend | S3 | P0 | `not started` | FR-1.17 |
| — | Customer interaction history (cross-ticket timeline) | Backend | S3 | S | `not started` | FR-1.17 |

---

## S5 — Message Record (customer contact history)

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-201 | Record inbound/outbound message against ticket | Backend | S5 | P0 | `not started` | FR-1.10 |

---

## S8 — Department & Branch Scoping

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-304 | Branch Foreign Keys | Backend | S8 | P0 | `not started` | FR-1.18 |
| — | Branch-scoped customer visibility | Backend | S8 | S | `not started` | FR-1.18 |
| — | Customer list filtered by branch | Frontend | S8 | S | `not started` | FR-1.18 |

---

## Summary

| Slice | Total Stories | Done | Not Started |
|---|---|---|---|
| S1 | 15 | 15 | 0 |
| S3 | 2 | 0 | 2 |
| S5 | 1 | 0 | 1 |
| S8 | 3 | 0 | 3 |
| **Total** | **21** | **15** | **6** |
