# Conversation record — ticket messages and their timeline

**Sprint:** 6 — Conversation record · **Feature:** `FEAT-14` · **Stories:** `US-201` (backend),
`US-202` (frontend) · **Epic:** [`EPIC-03` Communication channels](../../requirements/epics/EPIC-03-communication-channels.md)

## Problem

A ticket's status history (`TicketHistory`, `FEAT-08`) records *what happened* to a ticket —
created, assigned, status changed. It does not record *what was said* — a phone call, an email
received outside the system, an internal note about contact made. An agent working a ticket has
nowhere to log "spoke to the customer at 2pm, they confirmed the issue is still happening," and a
second agent picking up the same ticket has no way to see it happened.

This sprint delivers the record and its display only. Actual email send/receive integration —
provider, credentials, inbound webhook — is a separate, larger piece of work explicitly deferred to
sprint 9 (`EPIC-03`, `DEP-1`).

## Assumptions

A1. **`SenderId` always references the acting agent, including for `Direction = Inbound`.**
    Customers have no login and are not `Users`; an inbound message this sprint means an agent
    logging what a customer said (a phone call, a message received outside the system), not a
    customer-authored record. `SenderId` is never the customer.

A2. **`Channel` is agent-selected at creation time**, not inferred or defaulted. Nothing sends or
    receives real email this sprint — `Channel = Email` means "I am recording an email I sent or
    received outside the system," exactly as `Channel = System` means "this happened inside the
    app." The picker exists now so sprint 9's real ingestion has the same field to populate rather
    than needing a migration.

A3. **This is a separate entity from `CustomerNote`, not a generalisation of it.** Different
    aggregate root (`Ticket`, not `Customer`), different fields (`Direction`, `Channel`, `Subject`),
    different ordering (oldest-first, matching a conversation read top-to-bottom — `CustomerNote` is
    newest-first). Reusing `CustomerNote`'s table would mean an owner-type discriminator and a
    shape that serves two unrelated screens; a second small table is the smaller change.

A4. **The entity follows this codebase's `BaseEntity`/`Guid` convention, not the story's original
    SQL.** `US-201`'s SQL sketch used `BIGINT IDENTITY` with no soft-delete or audit columns —
    inconsistent with every shipped entity here (`CustomerNote`, `TicketHistory`, all
    `BaseEntity`-derived, `Guid` PK). The story's SQL predates the platform adoption (ADR-0009) and
    was never reconciled the way `docs/adr/0009`'s sibling stories already were.

A5. **Append-only, enforced by extending the existing `SaveChanges` guard** (ADR-0010), the same
    mechanism `TicketHistory` uses — not a new mechanism. `TicketMessage.Id` is left unassigned by
    `Create()` for the same reason `TicketHistory.Record()` leaves it unassigned: a client-assigned
    `Guid` on a row appended to an already-tracked `Ticket` makes EF mark it `Modified`, and the
    guard then refuses the append. This bug has hit this codebase twice already (`FEAT-06` deviation
    D1, `FEAT-08` deviation D1); the fix is to not repeat the cause a third time.

A6. **The read side is unpaginated**, like `TicketHistory` embedded in `TicketDetailDto` — not
    paginated like `CustomerNotes`. A message timeline is meant to render in full on one screen;
    nothing in this sprint's scope produces the message volume that would make paging necessary, and
    a pager control the frontend never needs is unbuilt scope.

A7. **`Body` is required, max 4000 characters** (matching `CustomerNote`'s limit — the same kind of
    free-text field). **`Subject` is optional, max 200 characters** (matching `Ticket.Subject`'s
    length, the closest existing analogue).

A8. **`SentAt` is server time at creation.** No backdating support this sprint — a message is
    recorded when it is logged, not asserted to have happened earlier. `ExternalMessageId` (the
    story's idempotency key for future inbound email processing) is out of scope until sprint 9
    actually ingests email; adding an unused column now is schema churn with no consumer.

## Out of scope

- Sending or receiving real email (sprint 9, `DEP-1`).
- Customer portal replies (sprint 10) — a customer authoring a message directly.
- Editing or deleting a recorded message (append-only, `BR-2`, same as ticket history).
- Attachments on a message (unrelated to `FEAT-13`'s customer attachments; not asked for here).
- Real-time push of new messages to other viewers (no SignalR wiring; the timeline reloads after a
  post, the same pattern `CustomerNote`'s screen uses).
- Pagination of the message list (A6).
- WhatsApp, live chat, SMS (deferred indefinitely, BRD §6.3).

## Acceptance criteria

AC-101. Given a valid ticket id and message details (`Direction` ∈ {Inbound, Outbound}, `Channel` ∈
{Email, System}, optional `Subject`, required `Body`), when an authenticated agent records a message,
then a `TicketMessage` row is stored with `TicketId`, the given `Direction`/`Channel`/`Subject`/`Body`,
`SenderId` set to the caller (never from the payload), and `SentAt` set to server time, and the
response is `201` with the new id.

AC-102. Given a `Body` that is empty or whitespace-only, when recording a message, then the response
is `400` with the error keyed to `Body`, and no row is stored.

AC-103. Given an unknown ticket id, when recording a message, then the response is `404`
(`TICKET_NOT_FOUND`), and no row is stored.

AC-104. Given a `Direction` or `Channel` value outside its allowed set, when recording a message,
then the response is `400` with the error keyed to the offending field.

AC-105. Given an unauthenticated request, when calling either the record or read route, then the
response is `401`.

AC-106. Given a ticket with recorded messages, when an agent reads its message timeline, then the
messages return ordered oldest-first by `SentAt`, each carrying the sender's display name (resolved
at read time, not stored — same arrangement as `TicketHistory`'s actor names).

AC-107. Given an unknown ticket id, when reading its message timeline, then the response is `404`.

AC-108. Given a ticket with zero recorded messages, when reading its message timeline, then the
response is `200` with an empty list — not `404`, and not indistinguishable from a failed read.

AC-109. A stored `TicketMessage` row can never be updated or deleted through any code path — proven
the same way `AC-49` proves it for `TicketHistory`: load a row, attempt to mutate and save, assert
the save throws.

AC-110 (frontend). Given a ticket detail screen with recorded messages, when it renders, then a
message timeline shows each message oldest-first with a visible direction indicator (Inbound/
Outbound), the sender's name, and its channel — positioned as its own section, distinct from the
status-change history timeline.

AC-111 (frontend). Given a ticket with no recorded messages, when the detail screen loads, then the
timeline shows an empty state, not a blank section indistinguishable from a loading or failed state.

AC-112 (frontend). Given the log-message form, when an agent submits a Direction, Channel and a
non-empty Body, then the client posts exactly those fields (plus optional Subject) and reloads the
timeline on success.

AC-113 (frontend). Given an agent submits the log-message form with an empty Body, then no request is
sent and a client-side validation message is shown under the Body field.

AC-114 (frontend). Given the server rejects a submission (e.g. `400`), then the timeline is
unaffected — the failed message is not optimistically added — and the server's message is shown
against the form.

## Design

### Backend: Domain

**New:** `CustomerSupport.Domain/Entities/Tickets/TicketMessage.cs` — a `BaseEntity` subclass:
`TicketId` (Guid), `Direction` (string, `"Inbound"`/`"Outbound"`), `Channel` (string,
`"Email"`/`"System"`), `Subject` (string?), `Body` (string), `SenderId` (Guid), `SentAt` (DateTime).
A private-setter `Create(...)` factory validates non-empty `Body` (≤4000 chars), `Subject` (≤200
chars if given), a non-empty `SenderId`, and that `Direction`/`Channel` are recognised values — the
same defence-in-depth `CustomerNote.Create` already applies, so a caller that bypasses the FluentValidation
layer (a test, a future internal caller) still cannot construct an invalid row. `Id` is left
unassigned for the reason in A5.

**New:** `IAppendOnlyEntity` marker interface in `Domain/Common`, implemented by `TicketHistory` and
`TicketMessage`. **Edit:** `AppDbContext.GuardAppendOnlyHistory()` — currently hardcoded to
`ChangeTracker.Entries<TicketHistory>()` (confirmed by reading the current implementation, not
assumed) — becomes `ChangeTracker.Entries<IAppendOnlyEntity>()`, so the same guard covers both
types without a duplicated check or a second near-identical method. A third append-only entity in a
future feature implements the interface and needs no `AppDbContext` change at all.

### Backend: Application

**New:** `Features/Tickets/Commands/RecordTicketMessage/` — `RecordTicketMessageCommand`,
its handler (loads the ticket by id for the 404 check, calls `TicketMessage.Create`, saves), and its
validator (`Body` `NotEmpty`/`MaximumLength(4000)`, `Subject` `MaximumLength(200)` when present,
`Direction`/`Channel` `Must(...)` against the allowed sets — rules against the properties directly,
per the `FEAT-03`/`MVP-05` lesson about `PropertyName` on invoked `Func`s).

**New:** `Features/Tickets/Queries/GetTicketMessages/` — `GetTicketMessagesQuery(TicketId)`, its
handler (404 if the ticket is absent, otherwise `ListOrderedAsync` ascending on `SentAt`, then
resolve sender display names once per distinct `SenderId` via `IIdentityUserService`, same pattern as
`GetTicketByIdQueryHandler`'s actor names). No `IQueryable` — this repository method already exists
post-refactor-sprint.

**Edit:** `ApplicationErrors.Ticket` gains no new entries beyond what already exists
(`NOT_FOUND` is reused); a new `MESSAGE_RECORDED` success code is added, plus
`Validation.MESSAGE_BODY_REQUIRED`, `Validation.MESSAGE_BODY_MAX_LENGTH`,
`Validation.MESSAGE_SUBJECT_MAX_LENGTH`, `Validation.MESSAGE_DIRECTION_INVALID`,
`Validation.MESSAGE_CHANNEL_INVALID` — each needs an `ar`/`en` pair in `Resources.yaml` or
`EveryErrorCode_HasABilingualMessage` fails the build.

### Backend: API

**Edit:** `TicketsController` — `POST /api/Tickets/{id:guid}/messages` (`201` + `Location`, pointed at
the list route, same reasoning `CustomerNotes` used: there is no single-message route because a
message is never fetched individually) and `GET /api/Tickets/{id:guid}/messages`
(`Response<IReadOnlyList<TicketMessageDto>>`). Both under the controller's existing
`[Authorize(Policy = "Authenticated")]`, XML-documented.

### Frontend

**Edit:** `common/src/lib/tickets/ticket.api.ts` — add `TicketMessage` interface, `listMessages(id)`
and `logMessage(id, request)` methods on `TicketApi`, following `listNotes`/`addNote`'s shape.

**Edit:** `admin-app/features/tickets/ticket-detail.component.*` — a new section below (or beside) the
existing history timeline: the message list (direction badge, sender, channel, subject/body,
timestamp), an empty state, and the log-message form (Direction select, Channel select, optional
Subject input, required Body textarea). Posting reloads the message list the same way `CustomerNote`'s
screen reloads notes after a successful add — no optimistic update (AC-114).

### Data model

One migration: `TicketMessages` table (Guid PK, FKs to `Tickets` and the identity `Users` table,
index on `(TicketId, SentAt)` for the ordered read). No changes to any existing table.

### Error behavior

No change to the envelope, status-code mapping, or existing error codes. New codes follow the
existing bilingual-catalogue and field-keyed-validation conventions exactly — no new error-handling
mechanism.
