# Epic 2 — Customer records

**Brief area:** 1 (Customer Management) — profiles, contact details, interaction history, notes and
attachments
**Why it matters:** a ticket without a customer is an anonymous complaint. This epic is what makes a
request belong to someone with a history.
**MVP scope:** all four bullets of brief area 1. **Interaction history is not optional** — a support
CRM that cannot record what was said is a to-do list.

---

## `MVP-03` — Record and find a customer

**As an** agent, **I want** to record who contacted us and find them again quickly, **so that** a
request can be attached to a person rather than an inbox.

**Status:** `done` — 2026-08-26. API (`AC-7`…`AC-13`) plus the list and create screens
(`AC-69`, `AC-70`). Closes the old `G-5` gap.

Verified against a running server, not only through `HttpTestingController`.

### Acceptance criteria

1. Given a name and email, I can record a customer and get back the created record.
2. Given a missing name, a malformed email or an over-length field, I am told **which field** is
   wrong — all of them in one answer, not one per attempt.
3. Given the email already belongs to a live customer, that is refused as a **conflict**, not as a
   malformed request.
4. Given many customers, I can page through them and search by name or email, case-insensitively.
5. Given a search that matches nothing, I see an empty list — never an error, and never a failure
   dressed as emptiness.

---

## `MVP-04` — Correct a customer's details, and retire one safely

**As an** agent, **I want** to fix a customer's details and remove records raised in error,
**so that** the data stays trustworthy without destroying support history.

**Status:** `done` — 2026-08-26. API plus the detail, edit and delete screens
(`AC-71`, `AC-72`, `AC-73`).

### Acceptance criteria

1. Given a valid change, it persists and is visible on re-reading.
2. Given an unknown customer, reading, changing or removing them answers "not found".
3. Given the new email belongs to another live customer, the change is refused as a conflict.
4. Given a customer **who has tickets**, removal is refused and the customer remains.
5. Given a customer with no tickets, removal succeeds, they vanish from listings, **and their email
   becomes reusable**.

### Notes

Criterion 4 is the one worth protecting: support history must not be destroyable by a mis-click.

Criterion 5 is why the uniqueness rule is filtered on live rows. Without it, a removed customer's
email is burned forever and the conflict points at a record nobody can see.

---

## `MVP-05` — Keep a customer's interaction history · **NOT BUILT**

**As an** agent, **I want** to record what was said and read it back newest-first, **so that** the
next person to speak to this customer knows what already happened.

**Status:** `done` — 2026-08-26. `AC-74`, `AC-75`, `AC-76`, built by two agents in parallel
against a contract fixed in the spec.

**Verified live**, which is what the criteria actually turn on: a POST carrying a forged `authorId`
*and* `createdBy` for another user was ignored — both notes came back attributed to the caller — and
a whitespace-only body was refused with 400.

### Acceptance criteria

1. Given a customer, I can add a note and it appears against them immediately.
2. Given several notes, they read **newest first** and are paged.
3. Given an empty note, it is refused before it is saved.
4. **The author is taken from my session, never from the request.** A payload attempting to set an
   author is ignored, not honoured.
5. Given a note, its author's name and the time it was written are shown.
6. Notes appear on the customer's own screen, not only through the API.

### Notes

Criterion 4 is a security criterion in a feature that looks purely functional. If the author can be
supplied by the caller, the interaction history becomes forgeable and worthless as a record.

Criterion 2's ordering is a database index (`IX_CustomerNotes_Customer_Created`), which already
exists — the entity and table shipped in Phase 0 and nothing consumes them.

---

## `MVP-06` — Attach what the customer sent · **NOT BUILT**

**As an** agent, **I want** to attach a screenshot or document to a customer, **so that** evidence
lives with the record instead of in my inbox.

**Status:** `done` — 2026-08-26. `AC-22`…`AC-28` and `AC-83`…`AC-85`.

**Verified live**, including all three defences:

| Upload | Result |
|---|---|
| valid PNG | 201, stored as a GUID name |
| `.exe` outside the allowlist | **415**, nothing written |
| filename `../../../etc/passwd.png` | 201 — stored as a GUID **inside** the root, original kept as metadata only; nothing escaped |
| 10 MB + 500 B | **413** `ATTACHMENT_TOO_LARGE`, nothing written |
| 11 MB | **400**, not 413 — see the boundary note below |

**Divergence from `AC-23`, found only by live testing.** The endpoint carries an outer
`RequestSizeLimit` of `MaxBytes + 1 MB` so a grossly oversized upload is cut off without being
buffered. A file *just* over 10 MB reaches the handler and gets the specified **413**; a file over
~11 MB trips the framework limit first and gets a **400**. That is sound defence in depth — you do
not want to buffer a 5 GB upload to produce a prettier status — but `AC-23` says "over the size
limit → 413" without qualification, so it is **not literally met above ~11 MB**.

The integration test passes because it uploads 10 MB + 1 byte, which stays inside the outer limit.
**No test covers the >11 MB boundary**, and this was invisible until the API was exercised for real.

### Acceptance criteria

1. Given a permitted file within the size limit, I can attach it and see its name, size and type.
2. Given a file over the limit, it is refused **and nothing is written to disk**.
3. Given a type outside the allowlist, it is refused. An **allowlist**, not a blocklist.
4. Given a hostile filename — `../../etc/passwd`, `..\\windows\\system32` — the stored file **cannot
   escape the storage directory**. The stored name is server-generated; the original is metadata.
5. Given an attachment, I can download it, and only with a valid session.
6. Given an attachment I no longer want, removing it takes the file off disk too.
7. Attachments appear on the customer's screen.

### Notes

Criteria 2, 3 and 4 are the reason this story is 1 point larger than it looks. Each is a distinct
attack, and criterion 4 is the one that reaches the filesystem.

The `Asset` / `CustomerAttachment` split already exists in the schema: `Assets` is the single
catalogue for every stored file, and ownership is a thin link. A future `TicketAttachments` reuses
the catalogue rather than altering it. `AC25_The_Stored_Name_Is_Server_Generated_And_Cannot_Escape_The_Directory`
already tests criterion 4 at the entity level.
