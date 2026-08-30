# Communication channels — WhatsApp, live chat, web forms and SMS conversations

**Epic:** [`EPIC-03` Communication channels](../../requirements/epics/EPIC-03-communication-channels.md)
**Features:** `FEAT-24` WhatsApp · `FEAT-25` SMS conversations · `FEAT-26` Live chat · `FEAT-27` Web forms
**Status:** Spec only — planning artifact. No implementation code exists for anything in this
document; see [`docs/superpowers/plans/EPIC-03-US-201-feat-24-communication-channels/`](../plans/EPIC-03-US-201-feat-24-communication-channels/)
for the backend task breakdown, and the note in [Reopening the deferral](#reopening-the-deferral)
below.

## Problem

Brief area 3 (Communication Channels) lists Email, WhatsApp, Live chat, SMS and Web forms. Area 11
(Integrations) repeats WhatsApp and SMS as integration targets. Of these:

- **Email** has committed delivery: message recording (`FEAT-14`), the notification gateway's
  outbound email adapter (`FEAT-15`), and inbound/outbound provider integration are specified and
  partly built.
- **SMS** exists today only as an *outbound system-notification* channel (OTP, SLA alerts) through
  `FEAT-15`'s `SmsNotificationChannelSender`. It has never been able to receive a reply, so a ticket
  cannot hold a real SMS conversation.
- **WhatsApp, Live chat, and Web forms have no design at all.** `EPIC-03` records them as "deferred
  indefinitely" (`BRD §6.3`) for stated reasons — a paid provider and verified business identity for
  WhatsApp, real-time staffing for live chat.

Nothing here is built. This spec exists so that when the deferral is lifted, work starts from a
reviewed design instead of a blank page.

## Reopening the deferral

This spec is written at explicit request (2026-08-27), which supersedes `EPIC-03`'s "deferred
indefinitely" status for WhatsApp, live chat and web forms. **It does not resolve the reasons the
deferral existed.** A paid WhatsApp Business provider account, its verified-identity approval, and
rostered live-chat staffing are business decisions outside this document's scope, and they are
carried forward as open questions (see A11) rather than assumed away. What this spec adds is a
design that can be built and tested against a sandbox/mock provider now, so that the only thing
blocking a production cut-over later is those business decisions — not a missing design.

**Per instruction, this pass produces the spec, the backend plan and its task breakdown only.** No
code is written, no test is run, and no frontend plan is written yet — consistent with this
project's own gate (`.claude/skills/sdd-workflow/SKILL.md`), which writes the frontend plan only
once the backend plan's tasks are actually implemented. `FEAT-18` (Knowledge base) is the precedent
for stopping at this boundary deliberately.

## Assumptions

Numbered so each can be checked or overturned without renumbering the acceptance criteria that cite
this section.

- **A1.** WhatsApp is delivered through the **WhatsApp Business Cloud API** (Meta), configured as a
  new `ExternalApiConfiguration` named `WhatsAppGateway` — same shape as the existing `EmailGateway`
  and `SmsGateway` (URL + protected credential via `ISecretProtector`). No provider SDK is added;
  it is another HTTP integration behind `IExternalApiConfigurationProvider`, matching `NG` spec A2.
  Twilio's WhatsApp API is the fallback if Cloud API onboarding is rejected — the configuration
  shape does not change either way.
- **A2.** SMS conversations reuse the existing `SmsGateway` configuration. The provider is assumed
  to support an inbound delivery webhook (Twilio and most SMS aggregators do); if the configured
  provider cannot deliver inbound webhooks, SMS stays outbound-only and `FEAT-25`'s inbound criteria
  do not apply — a provider capability check, not a design change.
- **A3.** Live chat is synchronous and session-based, delivered over SignalR. A session
  (`LiveChatSession`) is **not** a `Ticket` until an agent explicitly converts it (CC-17). No
  auto-promotion policy, no abandoned-session cleanup job, and no skills-based routing are in scope
  — a session sits in a flat "waiting" queue any available agent can claim.
- **A4.** Web forms are a **second, unauthenticated ticket-intake surface**, distinct from the
  customer portal (`FEAT-22`, `US-404`, "Customer Submits Ticket Through Portal" — authenticated).
  This spec's web form is what an anonymous visitor on a public marketing page fills in with no
  account; `US-404`'s portal submission is a signed-in customer's own flow. The two should not be
  merged into one endpoint — a portal customer submitting is already identified and the anonymous
  spam/rate-limit defenses in `CC-22` do not apply to them. If `FEAT-22` is built first, this
  feature reuses its `SubmitTicket` application layer with an anonymous entry point rather than
  duplicating validation — a task-level decision, not a spec change.
- **A5.** Customer matching on inbound contact: WhatsApp/SMS match by phone number, web forms match
  by email. If no `Customer` matches, one is created automatically with only the fields the channel
  provides (name + phone, or name + email) before the message/ticket is recorded. This resolves for
  these four channels the same shape of question `OQ-11` leaves open for email — decided here rather
  than left open, because unlike email these channels have no existing thread-matching convention to
  preserve.
- **A6.** One open ticket per `(CustomerId, Channel)` conversation. An inbound message on a channel
  that already has a non-terminal (`Status ∉ {Resolved, Closed}`) ticket from that customer on that
  channel is appended to it as a `TicketMessage`; otherwise a new ticket is created. Two channels
  never share one ticket automatically (a WhatsApp message and a web form from the same customer
  open two tickets) — merging is a manual agent action, not built here. **This needs `Ticket` to
  record which channel it originated on** — `Ticket` today has no such column (confirmed by reading
  `Domain/Entities/Tickets/Ticket.cs`; only `DepartmentId`/`BranchId` are precedent for a
  nullable "grouping" column nothing populates yet). This spec adds `Ticket.Source` (nullable
  `string`, set once at creation from the ingesting channel, `null` for every ticket created any
  other way) rather than matching through a `TicketMessage` join, which would incorrectly match on
  any channel a ticket had ever received a message on, not the channel it started on.
- **A7.** Inbound, channel-authored `TicketMessage` rows use `SenderId = SystemActors.ChannelIngestion`
  — a new well-known non-user actor, following the existing `SystemActors.EscalationEngine` pattern.
  `SystemActors` currently lives in `Infrastructure/Sla/SystemActors.cs`; since the new ingestion
  command lives in `Application` and `Application` must not reference `Infrastructure` (this
  project's one non-negotiable dependency rule), `SystemActors` relocates to `Domain/Common/` as
  part of this feature — it is two `Guid` constants with no `Infrastructure` dependency of its own,
  so the move is mechanical. This preserves the conversation-record spec's rule (A1 there) that
  `SenderId` never holds a customer identity, with no other schema change to `TicketMessage` beyond
  the idempotency column in `A6`'s design note below.
- **A8.** Outbound replies on any of these channels are dispatched only through
  `INotificationGateway`/`INotificationChannelSender` — never a direct provider call from a
  ticket-reply handler. `NotificationChannel` gains `WhatsApp`; `Sms` already exists.
- **A9.** Live chat carries no typing-indicator or read-receipt requirement. The BRD's stated
  blocker for live chat is staffing, not transport features — building more transport than the
  business can staff is scope the deferral reason does not ask for.
- **A10.** The web form's spam defense is a fixed per-IP-and-per-email submission throttle plus a
  honeypot field. A CAPTCHA provider is **not** assumed — see A11, `OQ-CC-1`.
- **A11 — open questions this spec does not close** (the business decisions the original deferral
  named): which WhatsApp provider account is actually purchased and verified (`OQ-CC-2`, seeds the
  already-open "Which WhatsApp provider?" row in `docs/product/05-assumptions-and-open-questions.md`);
  who staffs live chat and during what hours (`OQ-CC-3`); whether a CAPTCHA provider is approved for
  the web form (`OQ-CC-1`). None of these block writing or reviewing this spec; all of them block
  a production deployment of any of these three channels.
- **A12.** `MainHub` (`/hubs/main`) requires `RequireAuthorization("Authenticated")`
  (`Api.Shared/Extensions/WebApplicationExtensions.cs:70`, confirmed by reading the file, not
  assumed) and is mapped identically on both hosts. An anonymous live-chat visitor cannot use it.
  Live chat therefore adds a **second, narrow hub** for the anonymous side (see Design) rather than
  loosening `MainHub`'s policy — loosening it would let an anonymous connection join the `user:{id}`
  groups `FEAT-15`'s in-app notifications rely on.

## Out of scope

- ERP connectors and the AI chatbot — still deferred per `B5`/`BRD §6.3`; nothing here reopens them.
- A CAPTCHA integration (`OQ-CC-1`).
- Live chat auto-promotion-to-ticket, abandoned-session cleanup, and skills-based agent routing.
- WhatsApp template-approval tooling — templates are assumed pre-approved and configured; this spec
  sends and receives, it does not manage the Meta template catalogue.
- Historical backfill of any channel's prior conversations.
- Multi-department/branch routing of inbound channel messages (matches `FEAT-16`'s existing scope
  limits on `Ticket.DepartmentId`/`BranchId`, both nullable and unset by every existing path).
- Voice/phone calls — not named in the brief's channel list, not designed here.

## Acceptance criteria

### Shared inbound ingestion (used by WhatsApp, SMS, web forms)

**CC-1.** Given an inbound message identifying a customer by phone (WhatsApp/SMS) or email (web
form), when no matching `Customer` exists, then one is created with only the fields the channel
supplied, before the message is recorded.

**CC-2.** Given an inbound message from a channel with an existing non-terminal ticket for that
customer on that channel, when the message arrives, then it is appended to that ticket as a
`TicketMessage` rather than opening a new ticket.

**CC-3.** Given an inbound message from a channel with no non-terminal ticket for that customer on
that channel, when the message arrives, then a new `Ticket` is created (default category,
`Priority = Normal`, `Status = New`) with the message as its first `TicketMessage`.

**CC-4.** Given any inbound channel message, when it is recorded, then `TicketMessage.SenderId =
SystemActors.ChannelIngestion`, `Direction = Inbound`, `Channel` set to the originating channel, and
the row can never be updated or deleted (same proof as `AC-109`).

**CC-5.** Given a malformed or unverifiable inbound webhook payload (bad signature, missing required
field), when it is received, then the endpoint returns a safe rejection and no `Ticket`/`TicketMessage`
row is created.

### WhatsApp (`FEAT-24`)

**CC-6.** Given `WhatsAppGateway` configuration and a valid outbound dispatch request, when a
message is sent, then `WhatsAppNotificationChannelSender` (`SupportedChannel =
NotificationChannel.WhatsApp`) calls the configured integration URL with protected credentials
restored only at the transport boundary (same contract as `NG-2`).

**CC-7.** Given a transient WhatsApp provider failure, when dispatch is attempted, then the gateway
applies the same bounded retry policy as `NG-3` and never retries a permanent failure (`NG-4`).

**CC-8.** Given a signed inbound WhatsApp webhook payload, when it is received, then the shared
ingestion path (`CC-1`..`CC-4`) runs with `Channel = WhatsApp`.

**CC-9.** Given a duplicate WhatsApp provider message id (a retried webhook delivery), when it is
received twice, then only one `TicketMessage` is recorded — idempotent on
`(Channel, ProviderMessageId)`.

**CC-10.** Given an agent replies to a ticket whose latest inbound message came through WhatsApp,
when the reply is sent, then it dispatches through `INotificationGateway` with `Channels=[WhatsApp]`
and is recorded as an outbound `TicketMessage`.

### SMS conversations (`FEAT-25`)

**CC-11.** Given a signed inbound SMS webhook payload, when it is received, then the shared
ingestion path (`CC-1`..`CC-4`) runs with `Channel = SMS`, matching the customer by phone number.

**CC-12.** Given a duplicate inbound SMS provider message id, when received twice, then only one
`TicketMessage` is recorded (idempotent, same mechanism as `CC-9`).

**CC-13.** Given an agent replies to a ticket whose latest inbound message came through SMS, when
the reply is sent, then it dispatches through the existing `SmsNotificationChannelSender` and is
recorded as an outbound `TicketMessage`. (`FEAT-15` already delivers SMS as an outbound
*notification* channel; this closes the gap so a ticket reply — not only a system notification —
can leave by SMS, and an inbound SMS is recorded at all.)

### Live chat (`FEAT-26`)

**CC-14.** Given a customer opens the chat widget, when a session starts, then a `LiveChatSession`
is created in `Waiting` status with no ticket attached, and the client receives an opaque session
token scoped only to that session.

**CC-15.** Given a `Waiting` session, when an available agent claims it, then the session moves to
`Active`, is assigned to that agent, and both parties can exchange messages over the session's
group (`chat:{sessionId}`).

**CC-16.** Given an `Active` session, when either party sends a message, then it is delivered to the
other party in real time (no polling) and persisted against the session.

**CC-17.** Given an `Active` session, when the agent ends it, then the session moves to `Closed`;
the agent may, in the same action, convert it to a `Ticket`, carrying the transcript across as
ordered `TicketMessage` rows (`Channel = LiveChat`).

**CC-18.** Given a `Waiting` session with no agent claim within a configured timeout, then the
session is marked `Abandoned` and is **not** converted to a ticket automatically.

**CC-19.** Given an unauthenticated visitor and an authenticated agent, when either attempts to read
another session's transcript, then access is refused unless the requester is the assigned agent, an
admin, or holds that exact session's own token.

### Web forms (`FEAT-27`)

**CC-20.** Given a valid public submission (name, email, subject, description) to the anonymous
web-form endpoint, when it is posted, then the shared ingestion path (`CC-1`/`CC-3`, adapted to
match/create the customer by email) creates or appends to a ticket with `Channel = WebForm`, and the
response is `201` carrying only the ticket reference — no internal ids.

**CC-21.** Given a submission missing a required field or exceeding its length limit, when posted,
then the response is `400` with field-keyed errors, and no ticket is created.

**CC-22.** Given the honeypot field is filled or the per-IP/per-email rate limit is exceeded, when a
submission is posted, then the response looks identical to a successful submission (so the defense
is not signalled to an automated caller) but nothing is created — the attempt is logged for review,
not surfaced as a user-facing error.

**CC-23.** Given a successful web-form submission, when it completes, then a confirmation is
dispatched to the submitted email through the existing notification gateway (`Channels=[Email]`),
containing the ticket reference and no internal identifiers.

### Frontend (agent side, all channels)

**CC-24.** Given the ticket-detail message timeline (`FEAT-14`), when a message with `Channel ∈
{WhatsApp, SMS, WebForm, LiveChat}` is shown, then it renders with the same direction/sender/channel
treatment as `Email`/`System` today — the timeline component needs no per-channel special case.

**CC-25.** Given an open live-chat queue view, when an agent opens it, then waiting sessions list
with their wait time and a claim action; claiming navigates the agent into that session's chat view.

**CC-26.** Given an active chat session, when the agent sends a message, then it appears in the
transcript immediately (SignalR push), with no page reload or poll.

### Negative and security (cross-cutting)

**CC-27.** Given a webhook endpoint (WhatsApp or SMS) is called without a valid provider
signature/secret, when the request is received, then it is refused before any database write
(`401`/`403`), and the invalid payload is not logged in full.

**CC-28.** Given the live-chat and web-form endpoints are public/anonymous, when any request under
these features is made, then no route ever exposes another customer's data — access is scoped by
the matching in `A5`/`A6`/session-token ownership, never by a client-supplied customer id.

**CC-29.** Given any channel's inbound or outbound content, when it is persisted or logged, then
provider tokens, API keys and full webhook payloads are never included — same rule as `NG-5`.

## Design

### Why this design, not a bigger rebuild

Every one of these channels reuses a seam that already exists and was already built to be extended:
`INotificationChannelSender` (`NG-9`'s explicit design goal — "lets new channels register without
touching gateway code"), `TicketMessage`'s `Channel` field (already a string, already validated
against an allow-list in one place), and `IAppendOnlyEntity` (already generic over any entity, not
hardcoded to `TicketHistory`). Nothing here proposes a parallel mechanism.

### API host split

Per `ADR-0008`, the two hosts stay narrow and single-purpose:

| Surface | Host | Auth |
|---|---|---|
| WhatsApp/SMS inbound webhooks | `ExternalApi` | Anonymous + provider signature verification |
| Web form submission | `ExternalApi` | Anonymous + honeypot/rate-limit (`CC-22`) |
| Live chat — customer side (start session, send/receive) | `ExternalApi` | Anonymous + session token |
| Live chat — agent side (queue, claim, reply, convert) | `InternalApi` | `[Authorize(Policy="Authenticated")]`, same as every other staff action |
| WhatsApp/SMS ticket reply | `InternalApi` | Same |

### Domain

- **`TicketMessage.Channel`** allow-list extends from `{Email, System}` to `{Email, System,
  WhatsApp, SMS, WebForm, LiveChat}`. One `Must(...)` validator list, one migration-free change
  (it is a string column already).
- **`TicketMessage.ProviderMessageId`** (nullable `string`, new column) — the inbound idempotency
  key for `CC-9`/`CC-12`. A partial unique index on `(Channel, ProviderMessageId)` where not null
  enforces it at the database, not just in application logic (the same defense-in-depth pattern
  `TicketMessage.Create` already uses for its other invariants).
- **`SystemActors.ChannelIngestion`** — new well-known `Guid` constant beside `EscalationEngine`,
  used for `A7`.
- **`NotificationChannel`** gains `WhatsApp` (`Domain/ValueObjects/NotificationChannel.cs`), joining
  `InApp`/`Email`/`SMS`/`Push` in the same closed set with the same `Create`/`TryCreate` shape.
- **New aggregate `LiveChatSession`** (`Domain/Entities/Channels/LiveChatSession.cs`): `Id`,
  `Status` (`Waiting`/`Active`/`Closed`/`Abandoned`), `CustomerName`, `CustomerContact` (optional —
  a chat visitor may give none), `SessionToken` (opaque, generated at `Create`), `AssignedAgentId?`,
  `TicketId?` (set only on conversion), `CreatedAt`, `ClosedAt?`. Transitions mirror `Ticket`'s
  pattern: private setters, a small legal-transition table (`Waiting→Active→Closed`,
  `Waiting→Abandoned`), guard exceptions on illegal moves.
- **New `LiveChatMessage`** (append-only, implements `IAppendOnlyEntity`): `SessionId`, `SenderType`
  (`Customer`/`Agent`), `Body`, `SentAt`. Deliberately not `TicketMessage` — a chat message belongs
  to a session, not yet a ticket, and only migrates across as `TicketMessage` rows on conversion
  (`CC-17`), the same reasoning conversation-record spec's `A3` used to keep `TicketMessage` separate
  from `CustomerNote`.

### Application

- **`Features/Channels/Commands/IngestInboundChannelMessage/`** — one command, parameterized by
  `Channel`, shared by WhatsApp/SMS/web-form handlers after each does its own payload parsing and
  signature check. Implements `CC-1`..`CC-4`: resolve-or-create customer (`A5`), resolve-or-create
  ticket (`A6`), append `TicketMessage` with `SystemActors.ChannelIngestion` (`A7`). One handler, one
  set of tests, three thin controller actions that adapt each provider's payload shape into it.
- **`Features/Channels/Commands/ReplyToTicketViaChannel/`** — extends the existing ticket-reply path
  (used by `FEAT-14`'s message log for `Email`) to dispatch through `INotificationGateway` with the
  channel resolved from the ticket's latest inbound `TicketMessage.Channel`. No new reply UI concept
  — it is the same log-message form, now also triggering a real send for `WhatsApp`/`SMS`.
- **Live chat:** `StartChatSessionCommand`, `ClaimChatSessionCommand`, `SendChatMessageCommand`,
  `EndChatSessionCommand`, `ConvertChatSessionToTicketCommand`, `GetWaitingSessionsQuery`. The
  conversion command is the one place `LiveChatMessage` rows get copied into `TicketMessage` rows
  (`Channel = LiveChat`, `SenderId` = the agent for agent-authored, `SystemActors.ChannelIngestion`
  for customer-authored — same rule as `A7`).
- **Web form:** `SubmitWebFormTicketCommand` — validated (`CC-21`), rate-limited (`CC-22`, checked
  before validation so a throttled bot never reaches the validator), delegates to the shared
  ingestion command, then dispatches the confirmation email (`CC-23`).

### Infrastructure

- **`WhatsAppNotificationChannelSender`** — same shape as `EmailNotificationChannelSender`/
  `SmsNotificationChannelSender`: resolves `WhatsAppGateway` via `IExternalApiConfigurationProvider`,
  builds the configured URL, applies the same timeout/retry/secret-handling rules (`CC-6`/`CC-7`).
  Registered in `INotificationDispatcher` alongside the existing senders — no gateway code changes.
- **Webhook signature verification** — one `IWebhookSignatureVerifier` per provider family (WhatsApp
  uses Meta's `X-Hub-Signature-256`; SMS providers vary — Twilio uses `X-Twilio-Signature`).
  Verification runs before any database access (`CC-5`/`CC-27`); the raw payload is discarded from
  logs regardless of outcome (`CC-29`).
- **Live chat hub** — a **second, narrow SignalR hub** (`ChatHub`, `/hubs/chat`), mapped with
  `RequireAuthorization()` **omitted** deliberately (`A12`): the customer side never authenticates
  as a platform user. A connecting client presents its session token in the connection query string;
  `OnConnectedAsync` validates the token against an active `LiveChatSession` and joins only
  `chat:{sessionId}` — never `user:{id}`, so it cannot reach `FEAT-15`'s in-app notification groups.
  Agents keep using the existing authenticated `MainHub` and join `chat:{sessionId}` via its already
  generic `JoinGroup` call once they claim a session — no change to `MainHub` itself.

### Data model

Two migrations:

1. `TicketMessages` — add `ProviderMessageId` (nullable `nvarchar`) + partial unique index on
   `(Channel, ProviderMessageId)` where not null. Widen the `Channel` check/allow-list.
2. New tables `LiveChatSessions` (`Guid` PK, `Status`, `CustomerName`, `CustomerContact`,
   `SessionToken` unique index, `AssignedAgentId` FK nullable, `TicketId` FK nullable, timestamps)
   and `LiveChatMessages` (`Guid` PK, `SessionId` FK, `SenderType`, `Body`, `SentAt`, index on
   `(SessionId, SentAt)`). Both `BaseEntity`-derived, following the project's standing convention
   (`A4` in the conversation-record spec applies here identically).
3. `Tickets` — add `Source` (nullable `nvarchar`, no default, no backfill). Existing rows get
   `null`, which is a legitimate "not created from a tracked channel" value, not a data-migration
   problem (`A6`).

Only `Tickets` gets a column added; `FEAT-01`..`FEAT-23`'s other tables are unchanged.

### API and error contract

All HTTP endpoints use the existing `Response<T>` envelope, `IMessageFactory`, trace id, timestamp
and `ToActionResult(...)` mapping — no new envelope shape. New stable domain error keys: invalid
webhook signature, unsupported/unknown channel, session not found/not active, session token
mismatch, rate-limited submission (mapped internally, never surfaced as a distinguishable error per
`CC-22`). Each gets an `ar`/`en` pair in `Resources.yaml`, same as every existing error code — a
missing pair already fails the build (`EveryErrorCode_HasABilingualMessage`), so this is enforced
mechanically, not by review discipline.

### What this does not change

`INotificationGateway`, `INotificationDispatcher`, `NotificationGateway.SendAsync`, `MainHub`, and
every already-shipped channel sender (`Email`, `SMS` outbound, `InApp`) are used as-is. The only
edit inside `FEAT-15`'s existing code is registering one new sender
(`WhatsAppNotificationChannelSender`) and one new `NotificationChannel` value — exactly the extension
point `NG-9` was designed to absorb.
