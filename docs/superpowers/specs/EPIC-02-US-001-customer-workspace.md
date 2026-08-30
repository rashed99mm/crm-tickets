# Customer workspace — screens and interaction history

**Date:** 2026-08-26
**Stories:** `MVP-03`, `MVP-04`, `MVP-05` in
[`../../requirements/mvp/epic-2-customer-records.md`](../../requirements/mvp/epic-2-customer-records.md)
**Brief area:** 1 — Customer Management

## Problem

An agent can raise a ticket for a customer but cannot **look at** that customer. There is no customer
list, no detail screen, no way to correct a phone number. Customers exist in the UI only as a
`<select>` inside the ticket form.

And nothing anywhere records **what was said**. Brief area 1 names "interaction history"; the product
has none. The second agent to speak to a caller starts from nothing.

## Why this spec exists at all

`AC-7`…`AC-21` are approved and the API that satisfies them is built and tested. **What was never
specified is the frontend half** — recorded as gap `G-5`: *"the S1 spec defines no frontend criterion
for customer management screens."* It was raised, accepted, and never closed, which is how two
stories came to be counted as done while being invisible in the product.

This spec closes `G-5`. It **appends** `AC-69`…`AC-76`; nothing is renumbered.

## Assumptions

- **A11.** Notes are plain text. No formatting, no mentions, no editing after the fact — an
  interaction record that can be rewritten is not a record.
- **A12.** A note belongs to a customer, not to a ticket. Brief area 1 places "interaction history"
  under Customer Management. Ticket-scoped notes, if ever wanted, are a different feature.
- **A13.** Notes are never deleted, not even soft. `CustomerNote` carries the soft-delete columns
  from `BaseEntity`; no endpoint sets them.
- **A14.** Customer screens are for staff. Any authenticated user may read and write them — no
  criterion in the MVP restricts customer management by role.

## Out of scope

Attachments (`MVP-06`, its own story) · merging duplicate customers · customer-facing views ·
exporting · bulk import · per-note visibility or privacy · editing or deleting a note.

## Acceptance criteria

### Customer list and creation — `MVP-03`

- **AC-69** (P0) Given customers exist, when I open the customer list, then I see them paged with
  name, email and phone, and I can search by name or email. Loading, empty and error are three
  visually distinct states, and an empty **search** result says the search matched nothing rather
  than claiming there are no customers.
- **AC-70** (P0) Given the create form, when I submit an invalid one, then each server field error
  appears **on the control it names**; when I submit a valid one, the customer is created and I land
  on their detail screen. A duplicate email shows the server's conflict message, not a field error.

### Customer detail and correction — `MVP-04`

- **AC-71** (P0) Given a customer id, when I open their detail, then I see name, email, phone and
  when they were recorded. An unknown id shows a not-found state, not an empty form.
- **AC-72** (P0) Given the detail screen, when I change a detail and save, then it persists and is
  visible on reload. An email already held by another live customer shows the conflict message and
  the change is not applied.
- **AC-73** (P1) Given a customer **who has tickets**, when I try to remove them, then the server's
  refusal is shown as a message and the customer remains. Given one with no tickets, removal succeeds
  and returns me to the list.

### Interaction history — `MVP-05`

- **AC-74** (P0) Given a customer with notes, when I open their detail, then notes are listed
  **newest first**, each showing its author's name and when it was written.
- **AC-75** (P0) Given the note box, when I submit text, then the note appears in the list without a
  page reload. An empty or whitespace-only note is refused before any request is sent.
- **AC-76** (P0) The note's author is taken from the session. **The client never sends an author**,
  and a request that carries one has it ignored rather than honoured.

## Design

### API — already built, except notes

| Endpoint | State |
|---|---|
| `GET /api/Customers?page&pageSize&search` | **built** — `AC-10`, `AC-11`, `AC-13` |
| `GET /api/Customers/{id}` | **built** — `AC-12` |
| `POST /api/Customers` | **built** — `AC-7`, `AC-8`, `AC-9` |
| `PUT /api/Customers/{id}` | **built** — `AC-14` |
| `DELETE /api/Customers/{id}` | **built** — `AC-15`, `AC-16` |
| `GET /api/Customers/{id}/notes?page&pageSize` | **to build** |
| `POST /api/Customers/{id}/notes` | **to build** |

### The notes contract — fixed here so both halves can be built in parallel

```jsonc
// GET /api/Customers/{id}/notes?page=1&pageSize=20
{ "isSuccess": true, "traceId": "...", "error": null,
  "data": { "items": [ {
      "id": "…", "body": "Called back, awaiting logs.",
      "authorId": "…", "authorName": "Dana Support",
      "createdAt": "2026-08-26T09:00:00.000Z"
  } ], "pageIndex": 1, "pageSize": 20, "totalCount": 1 } }

// POST /api/Customers/{id}/notes
{ "body": "Called back, awaiting logs." }        // <- no author field exists (AC-76)
// 201 -> { "isSuccess": true, "data": { "id": "…", "message": {…} }, … }
```

Failure codes: `CUSTOMER_NOT_FOUND` (404, unknown customer in the **path**), `VALIDATION_ERROR`
(400, keyed to `Body`).

### Why `authorName` is projected, not stored

`CustomerNote` holds `AuthorId` only. The name is resolved at read time through
`IIdentityUserService`, the same arrangement ticket history uses — writing the name into the row
would freeze a value that changes and duplicate personal data into a table nothing can correct
(`A13`).

### Screens

```
/customers            list + search + "Add customer"     (AC-69)
/customers/new        create form                        (AC-70)
/customers/:id        detail + edit + delete + notes     (AC-71..AC-76)
```

The detail screen is one component holding three concerns — profile, actions, notes — because they
are one page to the agent. Notes are a child component so `MVP-06` can add attachments beside them
without touching the profile.

### Parallelism

The backend half (`/notes`) and the frontend half (all four screens) share no files: the contract
above is fixed, so they are built simultaneously and meet at the interceptor.

## Testing

| Level | Covers |
|---|---|
| Integration, real SQL Server | `AC-74`, `AC-75`, `AC-76` — ordering, refusal, and that a supplied author is ignored |
| Component, Vitest + `HttpTestingController` | `AC-69`…`AC-75` — the three async states, field-keyed errors, the conflict messages, no-reload append |
| Unit | already present — `CustomerNote.Create` refuses an empty body and a missing author |

`AC-76`'s test posts a body containing `authorId` and asserts the stored note's author is the
**caller**, not the value sent. A test that merely omits the field would pass against a handler that
honours it.
