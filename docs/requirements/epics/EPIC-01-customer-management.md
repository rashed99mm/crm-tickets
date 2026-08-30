# EPIC-01 · Customer management

| | |
|---|---|
| **Epic** | `EPIC-01` |
| **Priority** | P0 |
| **Stories** | 12 specified · 216-point plan share: 50 pts |
| **Sprints** | 2 (customers) · 5 (notes, attachments) |
| **Criteria** | AC-7…AC-28, AC-62, AC-65 — see [`../slice-s1-coverage.md`](../slice-s1-coverage.md) |

## Goal

A centralized customer profile containing customer information, contact information, support
history, notes, and attachments *(rule specification §8)* — realized here as validated CRUD with a
duplicate-email conflict, a delete guard that refuses to destroy support history, attributed notes,
and safely stored attachments.

## Why this epic exists

A ticket needs a customer to belong to, so this epic precedes ticket capture. Its two most
interesting rules are both refusals: a duplicate email is a conflict rather than a validation error,
and a customer holding tickets cannot be deleted at all. Notes are attributed from the session token,
never the payload (`BR-6`); attachments are allowlisted, size-capped before the stream is consumed,
and stored under server-generated names (`NFR-7`, `NFR-8`, `RSK-4`).

## Stories

| Story | Title | Priority | Points | Status | Criteria |
|---|---|---|---|---|---|
| [US-001](../user-stories/US-001-create-a-customer.md) | Create a customer with validated details *(rule proposal: Create Customer)* | P0 | 5 | `not started` | AC-7, AC-8 |
| [US-002](../user-stories/US-002-read-and-correct-a-customer.md) | Read and correct a customer's details *(rule proposals: View Profile · Update Customer)* | P0 | 3 | `not started` | AC-12, AC-14 |
| [US-004](../user-stories/US-004-find-a-customer.md) | Find a customer in a long list *(rule proposal: Search Customers)* | P0 | 5 | `not started` | AC-10, AC-11, AC-13 |
| [US-006](../user-stories/US-006-read-notes-newest-first.md) | Read a customer's notes newest first *(rule proposal: View Interaction History — note-level)* | P1 | 2 | `not started` | AC-21 |
| [US-007](../user-stories/US-007-record-a-note.md) | Record a note against a customer *(rule proposal: Add Customer Note)* | P1 | 5 | `not started` | AC-17…AC-20 |
| [US-008](../user-stories/US-008-attach-a-file.md) | Attach a file the customer sent *(rule proposal: Add Customer Attachment)* | P1 | 8 | `not started` | AC-22, AC-23, AC-24, AC-27 |
| [US-116](../user-stories/US-116-duplicate-email-is-a-conflict.md) | A duplicate customer email is a conflict, not a validation error | P0 | 3 | `not started` | AC-9 |
| [US-117](../user-stories/US-117-delete-guard.md) | A customer with history cannot be deleted | P0 | 5 | `not started` | AC-15, AC-16 |
| [US-130](../user-stories/US-130-notes-in-customer-detail.md) | Notes appear on the customer screen | P1 | 3 | `not started` | AC-62 |
| [US-131](../user-stories/US-131-hostile-filename-cannot-escape.md) | A hostile filename cannot escape the storage directory | P1 | 3 | `not started` | AC-25 |
| [US-132](../user-stories/US-132-retrieve-and-remove-attachment.md) | Retrieve and remove an attachment | P2 | 5 | `not started` | AC-26, AC-28 |
| [US-133](../user-stories/US-133-attachments-in-customer-detail.md) | Attachments appear on the customer screen | P2 | 3 | `not started` | AC-65 |

Absorbs former epics `EP-1.03` Customer records, `EP-1.10` Customer notes, `EP-1.11` Customer
attachments. US-002 secondarily realizes rule proposal *Manage Customer Contact Details* (US-003);
the cross-ticket interaction timeline of rule proposal US-006 remains open until later slices —
today's interaction history is the customer's notes.

## Reserved backlog (unspecified — titles only, no fabricated rules)

| Rule proposal | Future home | Note |
|---|---|---|
| US-005 Manage Customer Contact Details | folded into US-002 | contact fields are part of read/correct |
