# S1 — Ticket lifecycle

> **Superseded 2026-08-25 by the platform baseline.** The backend this document describes was
> replaced when the CCE Platform reference was adopted as the CRM baseline — see
> [`EPIC-12-US-000-crm-platform-baseline-design.md`](../specs/EPIC-12-US-000-crm-platform-baseline-design.md).
> The code named below no longer exists in `src/`; it is archived, not deleted. This file is kept
> because it is the record of what was built and why, and deleting it would erase the reasoning
> behind decisions that still hold — the envelope, the localisation approach and the dependency rule
> among them. **Do not follow its steps.**


**Date:** 2026-08-24
**Slice:** 1 of 8 (see `docs/assessment/brief.md` for the full decomposition)
**Brief areas covered:** 1 (Customer Management), 2 (Ticket Management), 4 (Agent Dashboard,
partial), 10 (Security & Administration, partial)

## Problem

A support agent has no single place to record who a customer is and what they have asked for.
Requests arrive and are tracked in inboxes and spreadsheets, so nobody can answer: which tickets
are mine, what state is this request in, who changed it last, and what has this customer already
told us?

Without that, work is duplicated, requests are dropped silently, and there is no record of who
did what to a ticket.

## Assumptions

Each is a question that could not be asked. Each is written so it can be proven wrong.

- **A1.** Agents are internal staff created by an administrator. There is no public
  self-registration in this slice.
- **A2.** Two roles are sufficient: `Agent` (works assigned tickets) and `Supervisor` (assigns
  and reassigns any ticket, manages customers).
- **A3.** A ticket belongs to exactly one customer and has at most one assignee.
- **A4.** Categories are a fixed seeded list, maintained by a developer, not editable in the UI
  in this slice.
- **A5.** Attachments belong to a customer, not to a ticket. The brief lists "Notes and
  attachments" under Customer Management.
- **A6.** Attachments are stored on the local filesystem. Object storage is a later concern and
  the storage port is defined so it can be swapped without touching handlers.
- **A7.** A single department and single branch. Multi-branch is slice S8.
- **A8.** English UI strings only. The i18n *mechanism* ships now; Arabic translation is S8.
- **A9.** Timestamps are stored and transmitted in UTC and rendered in the browser's timezone.
- **A10.** Deleting a customer who has tickets is refused rather than cascading. Support history
  must not be destroyable by a single click.

## Out of scope

Deliberate boundaries, so an assessor reads them as decisions rather than omissions. All are
placed in a later slice in the brief's decomposition.

Cross-ticket interaction timeline · tasks and reminders · quick replies · team collaboration ·
SLA targets and escalation rules · automatic assignment · notifications and alerts · all five
communication channels (email, WhatsApp, live chat, SMS, web forms) · knowledge base and search ·
all AI features · customer portal · reports and management dashboards · system-wide audit log
beyond ticket history · ERP and external integrations · multi-department and multi-branch ·
custom branding · Arabic translation · native mobile applications.

## UI mockups — what applies here

`stitch_smart_support_ticketing_crm/` holds thirteen generated screens covering the **product
vision**, not this slice. They span S1 through S7.

Inside S1: `ticket_queue`, `agent_dashboard_overview`, `customer_360_history`,
`customer_profile_history`, and the ticket-detail layout (minus its chatbot panel).

Outside S1, belonging to deferred slices: `ai_powered_agent_workspace`,
`ai_ticket_management_workspace`, `ticket_detail_chatbot` (S7),
`knowledge_base_management` (S4), `management_analytics_sla_performance` (S6 and S2),
`submit_ticket`, `user_dashboard` (S3), `admin_dashboard`, `admin_ticket_management` (S6, S10).

Those screens are reference, not a checklist. A screen not built in S1 is a scope decision
recorded here, not missing work.

## Acceptance criteria

`AC-n` ids are permanent. Tasks and tests cite them. New criteria are appended, never inserted.

Priority marks the cut order if time runs short: **P0** must ship, **P1** should ship, **P2** is
cut first. Cutting a P2 is a decision to record, not a silent omission.

### Authentication and authorization

- **AC-1** (P0) Given valid credentials, when logging in, then 200 with a JWT carrying the user
  id and role claims.
- **AC-2** (P0) Given invalid credentials, when logging in, then 401 with a message that does
  **not** reveal whether the account exists.
- **AC-3** (P0) Given a missing, malformed or expired token, when calling any protected endpoint,
  then 401.
- **AC-4** (P0) Given an `Agent` token, when calling a `Supervisor`-only endpoint, then 403.
- **AC-5** (P0) No endpoint, log line or error response ever contains a password or password hash.
- **AC-6** (P1) Given repeated failed logins beyond the configured threshold, then the account
  locks for the configured duration and further attempts return **401**, identical to a wrong
  password. Lockout state is deliberately not disclosed, for the same reason as AC-2 — a distinct
  status code would confirm the account exists.

### Customers

- **AC-7** (P0) Given name and email, when creating a customer, then 201 with a `Location`
  header and the created resource.
- **AC-8** (P0) Given a missing name, a malformed email, or a field over its length limit, when
  creating, then 400 with errors keyed by field name.
- **AC-9** (P0) Given an email already in use, when creating, then 409 naming the conflicting
  rule — not 400.
- **AC-10** (P0) Given customers exist, when listing, then `Response<PagedResult<CustomerDto>>` —
  `items`, `page`, `pageSize`, `totalCount` under `data`. **Amended 2026-08-24** by
  `EPIC-01-US-101-backend-foundation-design.md`.
- **AC-11** (P0) Given `pageSize` above the server maximum, when listing, then 400.
- **AC-12** (P0) Given an unknown id, when fetching, updating or deleting, then 404.
- **AC-13** (P1) Given a search term, when listing, then only customers whose name or email
  matches, case-insensitively.
- **AC-14** (P1) Given a valid update, then 200 and the change persists; validation matches
  AC-8.
- **AC-15** (P0) Given a customer with at least one ticket, when deleting, then 409 and the
  customer remains.
- **AC-16** (P1) Given a customer with no tickets, when deleting, then **200 with code `CON012`**
  and it is gone from listings. Soft-deleted, so the row survives and its email becomes reusable
  via the filtered unique index. **Amended 2026-08-24** — FND-5 replaces 204 with 200 so every
  response carries a code and message.

### Notes

- **AC-17** (P1) Given a body within its length limit, when adding a note to a customer, then 201
  and the note records author and creation time.
- **AC-18** (P1) Given an empty or whitespace-only body, then 400.
- **AC-19** (P0) The note's author is taken from the authenticated token and **never** from the
  request body. A body attempting to set an author is ignored, not honoured.
- **AC-20** (P1) Given an unknown customer, when adding a note, then 404.
- **AC-21** (P1) Given several notes, when listing, then newest first, paginated.

### Attachments

- **AC-22** (P1) Given a permitted file within the size limit, when uploading against a customer,
  then 201 with the stored metadata (id, original filename, size, content type).
- **AC-23** (P1) Given a file over the configured size limit, then 413 and nothing is written to
  disk.
- **AC-24** (P1) Given a content type outside the allowlist, then 415 and nothing is written.
- **AC-25** (P1) Given a filename containing path separators or traversal sequences
  (`../`, `..\`), then the stored path stays inside the configured directory. The stored name is
  server-generated; the original name is metadata only.
- **AC-26** (P2) Given an existing attachment, when downloading, then the correct content type,
  a `Content-Disposition` filename, and authentication required.
- **AC-27** (P1) Given an unknown customer, when uploading, then 404.
- **AC-28** (P2) Given an existing attachment, when deleting, then **200 with code `CON041`**, the
  metadata row is soft-deleted and the file is removed from disk. **Amended 2026-08-24** — FND-5
  replaces 204 with 200.

### Tickets

- **AC-29** (P0) Given subject, customer, category and priority, when creating a ticket, then 201,
  status `New`, a generated human-readable reference, and no assignee.
- **AC-30** (P0) Given a missing subject, an over-length field, or an invalid priority, then 400
  keyed by field.
- **AC-31** (P0) Given an unknown customer or category, when creating, then 400 identifying which.
- **AC-32** (P0) Given tickets exist, when listing, then a paginated envelope, newest first.
- **AC-33** (P0) Given filters for status, priority, assignee or customer, when listing, then only
  matching tickets; filters combine.
- **AC-34** (P0) Given the caller is an agent, when listing with the "mine" filter, then only
  tickets assigned to that caller.
- **AC-35** (P0) Given a ticket id, when fetching, then the ticket with a customer summary and its
  history, newest first.
- **AC-36** (P0) Given an unknown ticket id, then 404.

### Ticket status machine

The domain rule this slice is built around. Transitions live in the `Ticket` entity, not in
handlers.

Permitted: `New → Open` · `Open → Pending` · `Open → Resolved` · `Pending → Open` ·
`Pending → Resolved` · `Resolved → Closed` · `Resolved → Open` (reopen) · `Closed → Open`
(reopen). Everything else is refused.

- **AC-37** (P0) Given a permitted transition, when changing status, then 200 and the new status
  persists.
- **AC-38** (P0) Given a transition not in the table — `New → Closed`, `Closed → Resolved`,
  `New → Resolved` — then 409 naming the rule. Not 400: the request is well-formed, the state
  is wrong.
- **AC-39** (P0) Given a transition to the status the ticket already holds, then 409.
- **AC-40** (P1) Given a resolved or closed ticket, when reopening, then status becomes `Open` and
  the reopen is recorded in history.
- **AC-41** (P1) Given two callers changing the same ticket concurrently, then the second receives
  409 and the first change survives. No silent overwrite.

### Assignment and per-record authorization

The security showcase. Endpoint-level authorization is not sufficient for any of these.

- **AC-42** (P0) Given a `Supervisor`, when assigning a ticket to an agent, then 200 and the
  assignee changes.
- **AC-43** (P0) Given an `Agent`, when assigning any ticket, then 403 — including a ticket
  assigned to themselves.
- **AC-44** (P0) Given a target user who does not exist or is not an agent, when assigning, then
  400.
- **AC-45** (P0) Given an `Agent`, when changing the status of a ticket **not** assigned to them,
  then 403 and the ticket is unchanged.
- **AC-46** (P0) Given an `Agent`, when changing the status of their own assigned ticket, then
  200.
- **AC-47** (P1) Given a `Supervisor`, when changing the status of any ticket, then 200.

### Ticket history

- **AC-48** (P0) Given a ticket is created, assigned, reassigned, or has its status changed, then
  a history row is appended recording actor, UTC timestamp, the change type, and the from/to
  values.
- **AC-49** (P0) History is append-only. No endpoint updates or deletes a history row, and none
  is exposed to do so.
- **AC-50** (P1) Given a ticket with history, when fetching it, then entries are returned newest
  first with the actor's display name.

### Cross-cutting API behaviour

> **Amended 2026-08-24** by `EPIC-01-US-101-backend-foundation-design.md`. The unified response
> envelope replaces RFC 9457 `ProblemDetails` — see ADR 0004 for the decision and its cost. HTTP
> status codes remain meaningful; the envelope travels in the body of a correctly-statused
> response.

- **AC-51** (P0) Every response — success and failure — is the envelope
  `{ success, code, message: { ar, en }, data, errors[], traceId, timestamp }`. Validation
  failures carry top-level `VAL001` and one `errors[]` entry per field.
- **AC-52** (P0) No response body ever contains a stack trace, SQL text, or connection string.
- **AC-53** (P1) Every response carries `traceId` from `Activity.Current`, matching the server log
  for that request. An unhandled exception returns 500 with code `ERR900` and a generic message.
- **AC-54** (P1) Dates on the wire are ISO 8601 UTC; JSON properties are `camelCase`.
- **AC-66** (P0) The status codes named throughout this spec carry these codes: AC-9 → `ERR011`,
  AC-15 → `ERR012`, AC-38/AC-39 → `ERR021`/`ERR022`, AC-41 → `ERR024`, AC-45 → `ERR023`,
  AC-23 → `ERR051` (413), AC-24 → `ERR052` (415).
- **AC-67** (P0) Account lockout (AC-6) returns the **same** code and message as invalid
  credentials (AC-2). No distinct lockout code exists, because one would confirm the account
  exists.

### Frontend

- **AC-55** (P0) Given valid credentials on the login form, then the user reaches the ticket list;
  given invalid, a visible error appears and no navigation occurs.
- **AC-56** (P0) Given no session, when opening a protected route directly, then redirect to
  login.
- **AC-57** (P0) Given the ticket list, then paged results with a working status filter and a
  "my tickets" toggle.
- **AC-58** (P0) Loading, empty and error states are visually distinct on every data view. An
  empty result never looks like a failure, and a failure never looks empty.
- **AC-59** (P0) Given the create-ticket form, then client validation mirrors the server's rules,
  errors appear only after touch, and submit is disabled while invalid **and** while in flight.
- **AC-60** (P0) Given the server returns `errors[]`, then each entry maps onto the form control
  named by its `field` (camelCase, matching the request DTO), not into a generic banner. The
  top-level `message` may additionally be shown as a summary. **Amended 2026-08-24** by
  `EPIC-01-US-101-backend-foundation-design.md`.
- **AC-68** (P1) The active locale selects `ar` or `en` from each response's `message` object; no
  refetch occurs when the language is switched.
- **AC-61** (P0) Given ticket detail, then customer summary, history timeline, and the status
  action are shown; the assign action is hidden for agents **and** refused by the server if
  called anyway.
- **AC-62** (P1) Given a customer detail view, then its notes are listed newest first and a note
  can be added through a validated form.
- **AC-63** (P1) No user-facing string is hardcoded in a template; text resolves through the i18n
  mechanism and the document `dir` follows the active locale.
- **AC-64** (P1) One Playwright journey: log in, create a ticket, assign it, change its status,
  reload, and confirm the change and its history persisted.
- **AC-65** (P2) Given a customer detail view, then attachments are listed and a file can be
  uploaded with client-side size and type checks before submitting. Depends on AC-22 and AC-26,
  so it is cut with them.

## Design

### Architecture

The four-project Clean Architecture layout from `CLAUDE.md`, dependencies pointing inward only.

```
src/Domain/          Ticket, Customer, Note, Attachment, TicketHistory, status machine
src/Application/     one folder per use case; port interfaces; validators
src/Infrastructure/  EF Core, Identity, file storage, JWT issuing
src/Api/             endpoints, DI, middleware
tests/               Domain.Tests, Application.Tests, Api.IntegrationTests
frontend/            Angular 20 workspace
```

`Domain` holds the status machine and every invariant, with no EF or Identity types. This matters
beyond tidiness: `Ticket.ChangeStatus` must be unit-testable with no database, because AC-37
through AC-41 are the most-tested logic in the slice.

### Data model

| Entity | Notable columns | Notes |
|---|---|---|
| `AppUser` | Identity-provided, plus `DisplayName` | ASP.NET Core Identity schema |
| `Customer` | `Name`, `Email` (unique), `Phone`, `CreatedAtUtc` | Unique index on email backs AC-9 |
| `Category` | `Name`, `IsActive` | Seeded, read-only in S1 |
| `Ticket` | `Reference` (unique), `Subject`, `Description`, `CustomerId`, `CategoryId`, `Priority`, `Status`, `AssigneeId?`, `RowVersion` | `RowVersion` backs AC-41 |
| `TicketHistory` | `TicketId`, `ActorId`, `ChangeType`, `FromValue`, `ToValue`, `OccurredAtUtc` | Append-only |
| `CustomerNote` | `CustomerId`, `Body`, `AuthorId`, `CreatedAtUtc` | Author from token (AC-19) |
| `CustomerAttachment` | `CustomerId`, `OriginalFileName`, `StoredFileName`, `ContentType`, `SizeBytes`, `UploadedById` | Stored name is a GUID |

> **Amended 2026-08-25** by `EPIC-12-US-000-s1-schema.md`. The `CustomerAttachment` row above —
> carrying `OriginalFileName`, `StoredFileName`, `ContentType`, `SizeBytes`, `UploadedById`
> directly — is superseded. The schema spec splits it into two tables: `Assets`, a file catalogue
> holding that metadata as the single point of entry for every stored file, and
> `CustomerAttachments`, a thin ownership link carrying only `CustomerId` and a unique `AssetId`.
> Rationale: a future `TicketAttachments` join table can reuse the same catalogue without altering
> it. Rendered view: [architecture/erd.md](../../architecture/erd.md).

`Status` and `Priority` are domain enums persisted as strings — readable in the database and
resistant to the reordering bug that renumbers every existing row when persisted as integers.

`Reference` is generated as `TKT-` plus a zero-padded sequence. It exists because "ticket 4192"
is not something a person reads aloud to a customer.

### Concurrency

`RowVersion` on `Ticket` gives optimistic concurrency. EF throws
`DbUpdateConcurrencyException`, which one middleware translates to 409 (AC-41). This is worth the
column: two agents resolving the same ticket is ordinary, and a silent last-write-wins loses an
audit entry.

### Status machine

A static transition table in `Domain`, consulted by `Ticket.ChangeStatus(newStatus, actorId)`,
which either appends a history entry and mutates state, or returns a failure. Setters are
private — a public `Status` setter would let any handler bypass the table, and eventually one
would.

### Errors

The unified envelope plus one exception-handling middleware, with `MessageType` mapped to status in
a single place (FND-4). The mapping, which is also the contract the frontend codes against:

| Condition | `MessageType` | Status | Code |
|---|---|---|---|
| Input shape invalid | `Validation` | 400 | `VAL001` + per-field `VAL0xx` in `errors[]` |
| Unauthenticated, or locked out | `Unauthorized` | 401 | `ERR002` — same for both, deliberately |
| Authenticated but not permitted | `Forbidden` | 403 | `ERR005`, `ERR023` |
| Record absent | `NotFound` | 404 | `ERR010`, `ERR020`, … |
| Well-formed request, wrong state (bad transition, duplicate email, customer has tickets) | `Conflict` | 409 | `ERR011`, `ERR012`, `ERR021`, `ERR022`, `ERR024` |
| File too large | `PayloadTooLarge` | 413 | `ERR051` |
| Content type not allowed | `UnsupportedMediaType` | 415 | `ERR052` |
| Unexpected | `Internal` | 500 | `ERR900`, generic message, `traceId` only |

The 400-versus-409 split is deliberate and tested. A duplicate email is not a malformed request.

**404 versus 400 for a missing related record** follows one rule, so the apparent conflict
between AC-31 and AC-12 is not one. A resource named in the *path* that does not exist is 404 —
`GET /customers/{unknown}`, or posting a note to an unknown customer. A resource referenced in a
*request body* that does not exist is a 400 field error, keyed to that field — `customerId` on
ticket creation — because the addressed resource (the ticket collection) does exist and the
payload is what is wrong.

### Attachment storage

An `IFileStore` port in `Application`, implemented in `Infrastructure` over the local filesystem,
rooted at a configured directory **outside** the web root. Files are never served from a static
path — a download endpoint streams them after authorizing the caller.

Defences, each with its own test: a GUID stored filename so the original never touches the
filesystem (AC-25), a size cap checked before the stream is consumed (AC-23), a content-type
allowlist rather than a blocklist (AC-24), and the resolved path asserted to sit under the root
before any write.

### Authorization

Two layers, both required. Policies on endpoints for role checks (AC-4, AC-43), and an explicit
ownership check inside the handler for per-record rules (AC-45, AC-46). The handler check cannot
be replaced by the policy: only the handler has loaded the ticket and can see who it is assigned
to.

### Frontend

Angular 20 standalone with signals, feature folders under `src/app/features/{auth,tickets,
customers}`, lazy-loaded routes, `OnPush` throughout. Typed reactive forms. Two interceptors:
one attaching the token, one unwrapping the response envelope so `data` reaches components and
`errors[]` reaches form controls by `field` (AC-60). Because the envelope is uniform, the
interceptor is the only place that knows about it — services and components see typed data or a
typed error, never `success` flags.

Every data view models loading / loaded / empty / error as distinct states. AC-58 exists because
`catchError(() => of([]))` is the default mistake here and it renders a server failure as "no
tickets".

The i18n foundation is a translation dictionary plus a locale signal driving `dir`. English only,
but no string is hardcoded, so S8 adds a file rather than editing every template.

### Testing

| Level | Covers |
|---|---|
| `Domain.Tests` | Status machine — every permitted transition, every refused one, self-transition. Fast, no infrastructure. |
| `Application.Tests` | Handlers with ports faked: authorization branches, history appended on each change, author taken from token not body. |
| `Api.IntegrationTests` | `WebApplicationFactory` with SQL Server via Testcontainers: real auth, status codes, response shapes, the 400/409 split, upload limits, concurrency. |
| Frontend | Component tests for the three async states and form validation; `HttpTestingController` asserting method, URL and body. |
| E2E | One Playwright journey (AC-64). |

`UseInMemoryDatabase` is not used anywhere: AC-9 depends on a unique index and AC-41 on
`RowVersion`, neither of which the in-memory provider honours. It would pass both tests while
the real database failed.

Docker is present (27.3.1) but `docker run` has been unreliable on this machine, so verify
Testcontainers starts before committing the suite to it. LocalDB with a per-run database name is
the fallback.

## Build order and cut lines

Scope was chosen over my advice to keep it narrower: full ASP.NET Core Identity plus notes,
attachments and customer CRUD is realistically four to five days, not the two to three available.
The order below exists so that running out of time removes a whole feature cleanly instead of
leaving several half-built.

1. Solution skeleton, Identity, login, JWT, seeded roles and users — **AC-1 to AC-6**
2. Customers CRUD with validation and the delete guard — **AC-7 to AC-16**
3. Tickets: create, list, filter, detail — **AC-29 to AC-36**
4. Status machine and history — **AC-37 to AC-41**, **AC-48 to AC-50**
5. Assignment and per-record authorization — **AC-42 to AC-47**
6. Frontend: login, list, detail, create form — **AC-55 to AC-61**
7. Notes, API and UI — **AC-17 to AC-21**, **AC-62**
8. Attachments, API and UI — **AC-22 to AC-28**, **AC-65**
9. i18n foundation and the Playwright journey — **AC-63, AC-64**

Steps 1–6 are the defensible core: they cover every rubric criterion on their own. **Cut from the
bottom — step 8 (attachments) first, then step 7.** Any cut gets recorded in
`docs/assessment/rubric-traceability.md` as a scope decision, not left as an unexplained gap.

## Decisions to record as ADRs

- Four-project Clean Architecture over vertical slices
- ASP.NET Core Identity over a minimal hand-rolled JWT (chosen against recommendation; the
  reasoning belongs on the record)
- Status machine in the domain entity rather than in handlers
- `RowVersion` optimistic concurrency
- Local filesystem attachment storage behind a port
- SQL Server over PostgreSQL
