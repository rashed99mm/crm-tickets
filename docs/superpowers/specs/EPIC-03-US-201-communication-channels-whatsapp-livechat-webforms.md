# Communication channels — WhatsApp, live chat, web forms and SMS conversations

**Epic:** [`EPIC-03` Communication channels](../../requirements/epics/EPIC-03-communication-channels.md)
**Features:** `FEAT-24` WhatsApp · `FEAT-25` SMS conversations · `FEAT-26` Live chat · `FEAT-27` Web forms ·
`FEAT-35` Mock provider gateway and the mock/real toggle
**Status:** Outbound + the mock/real toggle (`CC-1`–`CC-9`, `CC-30`–`CC-51`) implemented and verified —
see [`EPIC-03-US-201-feat-35-channel-mock-gateway`](../plans/EPIC-03-US-201-feat-35-channel-mock-gateway/README.md)
for the full task-by-task record, including `CC-51`'s fix for the outbound-send defect this header
used to describe as red. **Not yet committed** — implemented and verified this session, commit pending.
Inbound completion (`CC-40`–`CC-44`, the revised `CC-47`) is specced in
[Amendment — 2026-09-02, inbound completion](#amendment--2026-09-02-inbound-completion) below and is
now **implemented and verified, also uncommitted** — see
[`EPIC-03-US-201-feat-35-inbound-completion`](../plans/EPIC-03-US-201-feat-35-inbound-completion/README.md),
including `A27`, which corrects this amendment's own mistaken claim that `CC-44` was a one-line fix.
`CC-45`/`CC-46` (live chat, abandoned session) remain deferred by instruction. See
[Amendment — 2026-09-02](#amendment--2026-09-02-mock-provider-gateway-mockreal-toggle-and-email-inbound)
for the plan-1 verified build state, and
[`docs/superpowers/plans/EPIC-03-US-201-feat-24-communication-channels/`](../plans/EPIC-03-US-201-feat-24-communication-channels/)
for the original `FEAT-24` task breakdown.

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

> **Superseded 2026-09-02.** That last paragraph was true when written and is no longer: the shared
> ingestion path, all of WhatsApp, and the SMS/WhatsApp reply branch are built. The
> [amendment](#amendment--2026-09-02-mock-provider-gateway-mockreal-toggle-and-email-inbound)
> carries the state verified against the code. The paragraph is left in place rather than rewritten
> so the record shows what was believed when the design was approved.

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

---

## Amendment — 2026-09-02: mock provider gateway, mock/real toggle, and email inbound

Written at explicit request. Three things are added to this feature: a **mock API per channel** in
the `cms-integration-gateway` project, a **configuration toggle** that decides whether the platform
talks to those mocks or to real providers, and **inbound email**, which the original pass left out.

`CC-1`–`CC-29` and `A1`–`A12` above are unchanged. Criteria ids are permanent, so this amendment
appends from `CC-30` and assumptions from `A13`.

### Verified build state (read from the code on 2026-09-02, not from the plan record)

Both the header of this spec and the plan's `README.md` claimed nothing was implemented. Both were
wrong. What is actually there:

| Criteria | Verified in code | State |
|---|---|---|
| `CC-1`–`CC-5` shared inbound ingestion | `Application/Features/Channels/Commands/IngestInboundChannelMessage/*`; 8 tests in `Integration/IngestInboundChannelMessageTests.cs` named `CC1_`…`CC4_` | **built** |
| `CC-6`–`CC-7` WhatsApp outbound | `Infrastructure/Notifications/WhatsAppNotificationChannelSender.cs`; 7 tests named `CC6_`/`CC7_`, all green | **built at unit level only** — see `A19` |
| `CC-8`–`CC-9` WhatsApp inbound | `ExternalApi/Controllers/WhatsAppWebhookController.cs`, `Infrastructure/Channels/MetaSignatureVerifier.cs`; `Integration/WhatsAppWebhookTests.cs` | **built** |
| `CC-10` WhatsApp reply | the `is "WhatsApp" or "SMS"` branch in `RecordTicketMessageCommandHandler.cs:72` | **NOT delivered** — `Integration/WhatsAppOutboundReplyTests.CC10_WhatsAppReply_RecordsOutboundMessageAndDispatchesToTheGateway` is **red**; see `A19` |
| `CC-13` SMS reply | same branch | **NOT delivered** — same root cause as `CC-10`, same code path |
| `CC-14`–`CC-17`, `CC-19` live chat | `Domain/Entities/Channels/LiveChatSession.cs`, `LiveChatMessage.cs`, `ChatController` on both hosts, `ChatHub`; frontend tasks 07–11 recorded complete | **partly built** |
| `CC-18` abandoned-session timeout | nothing found | **missing** |
| `CC-11`–`CC-12` SMS inbound | no SMS webhook controller exists | **missing** |
| `CC-20`–`CC-23` web-form backend | frontend widget complete; no anonymous backend endpoint exists | **missing** |

`"SMS"` and `"WebForm"` sit in the inbound allow-list with no transport that can reach them — they
are reachable only by sending the command in-process, which is what the integration tests do.

- **A19 — outbound sending is broken today, and this was found by running the tests rather than
  reading them.** The first version of this amendment listed `CC-10` as built, inferred from the
  existence of tests named `CC10_`. Those tests were then executed:
  `WhatsAppOutboundReplyTests.CC10_WhatsAppReply_RecordsOutboundMessageAndDispatchesToTheGateway`
  **fails** with `Expected _stub.ReceivedBodies to contain 1 item(s), but found 0` — the HTTP POST
  never leaves the process.

  Root cause, confirmed by reading the three files involved: `DatabaseExternalApiProvider.MapToConfig`
  (`ExternalApis/Providers/DatabaseExternalApiProvider.cs:96-101`) **already decrypts** every
  credential through `Decrypt(...)`, so `GetConfig` hands back plaintext. Each sender's `ApplyAuth`
  then calls `_secretProtector.Unprotect(...)` on that plaintext (or on `string.Empty` when the
  column was null), and `DataProtectionSecretProtector.Unprotect` delegates straight to
  `IDataProtector.Unprotect`, which throws on input that is not a valid protected payload. Because
  `ApplyAuth` is invoked *outside* the retry `try` block (`WhatsAppNotificationChannelSender.cs:61`),
  the exception escapes `SendAsync` and is swallowed by `NotificationGateway.cs:93-99`, which logs
  and records `DELIVERY_FAILED`.

  The consequence is wider than WhatsApp: **every** outbound Email, SMS and WhatsApp dispatch
  through a database-configured gateway fails, for any `AuthType` other than `None`, and presents
  as a provider outage. The seven WhatsApp unit tests pass only because they inject
  `IdentitySecretProtector`, whose `Unprotect` is the identity function — the double-unprotect is
  invisible at unit level by construction.

  `CC-51` below covers the fix. It is sequenced first in the plan, because provider-faithful
  adapters built on a path that never posts would be untestable end-to-end.

### Assumptions

- **A13.** The `cms-integration-gateway` (Node/Express + json-server, port 3001, model-driven via
  `models/*Model.js` → `ServiceRegistry.js` → `behaviors/*-rules.js` → `mocks/{group}/{name}.json`)
  is a **development and demonstration surface only**. It is never deployed, it has no
  authentication by its own design, and **it is not a test dependency** — the backend suite must
  pass with port 3001 closed (`CC-50`). Its existing `email` and `sms` mocks use a house shape
  (`POST /integrationgateway/{svc}/send`) which nothing in the backend actually calls today; the
  new provider-faithful routes are added alongside them rather than replacing them, so nothing that
  currently consumes the house routes breaks.
- **A14.** SendGrid, Meta Cloud API and Twilio are impersonated as **contract references, not
  procurement decisions**. Choosing their shapes commits the project to nothing; `OQ-CC-2` (which
  WhatsApp provider is actually purchased and verified) stays open exactly as `A11` left it. The
  adapter seam in `CC-49` is what keeps the real choice a configuration change — which is the whole
  reason to impersonate a real contract instead of inventing one.
- **A15.** The toggle is a **single global boolean**. Per-channel mock flags (real email, mocked
  WhatsApp) are deliberately not built; the configuration shape can grow into a dictionary later
  without breaking the flag.
- **A16.** Live chat and web forms have **no third party to impersonate** — live chat's transport is
  this platform's own SignalR hub and a web form is an anonymous HTML POST to this platform's own
  endpoint. Their gateway-side artifact is therefore a **client simulator** (a scripted visitor, a
  scripted form poster), not a served provider API. Calling those "provider mocks" would model a
  system that does not exist.
- **A17.** Inbound email matches the customer **by email address**, resolving for email the same
  question `A5` resolved for the other channels and `OQ-11` left open. Threading follows `A6`
  unchanged — `Ticket.Source = "Email"`, one open ticket per `(CustomerId, Channel)`.
- **A18 — resolving a contradiction already in this spec.** `A3` states that "no abandoned-session
  cleanup job" is in scope, while `CC-18` requires that a `Waiting` session past a configured
  timeout **is** marked `Abandoned`. Nothing can satisfy `CC-18` without something running on a
  timer, so the two cannot both hold. Resolved in favour of `CC-18`, narrowly: a job marks the
  session `Abandoned` and does nothing else — no ticket conversion (`CC-18` forbids it), no
  customer notification, no requeue. `A3`'s exclusions of auto-promotion and skills-based routing
  stand. This is a defect in the approved spec found while amending it, recorded rather than
  quietly patched, because `CC-18` was counted as "specified" in the plan while being impossible to
  implement as written.

### Acceptance criteria

#### The toggle (`FEAT-35`)

**CC-30.** Given `Channels:UseMocks` is absent or `false`, when any channel gateway configuration is
resolved, then it resolves from the database exactly as it does today and no mock route is contacted.

**CC-31.** Given `Channels:UseMocks` is `true`, when `EmailGateway`, `SmsGateway` or
`WhatsAppGateway` is resolved, then the resolved configuration carries the mock base URL and the
dev credential; and when any other named configuration is resolved (`WeatherApi`, `PaymentGateway`,
the ERP client), then it still resolves from the database unchanged.

**CC-32.** Given `Channels:UseMocks` is `true` and the environment is `Production`, when the host
starts, then startup fails with a message naming the flag, and no request is served.

**CC-33.** Given mocks are active and no `EmailGateway`/`SmsGateway`/`WhatsAppGateway` row exists in
the database, when a dispatch is attempted, then it succeeds — the flag, not a hand-created row, is
what a fresh clone needs. (Today all three senders return `CONFIG_MISSING`, and no such row is
created anywhere in `src`; only `Integration/GatewayTestData.cs` seeds one.)

#### Provider-faithful outbound (`FEAT-35`)

**CC-34.** Given mocks are active, when an email is dispatched, then the request is SendGrid v3's
contract — `POST /mock/sendgrid/v3/mail/send` with a `personalizations`/`from`/`subject`/`content`
body — and the provider message id is read from the `X-Message-Id` response header of a `202` with
an empty body.

**CC-35.** Given mocks are active, when a WhatsApp message is dispatched, then the request is Meta
Cloud API's contract unchanged from what `WhatsAppNotificationChannelSender` already emits, and the
provider message id is read from `messages[0].id`.

**CC-36.** Given mocks are active, when an SMS is dispatched, then the request is Twilio's contract —
form-encoded `To`/`From`/`Body` to `POST /mock/twilio/2010-04-01/Accounts/{sid}/Messages.json` — and
the provider message id is read from `sid`.

**CC-37.** Given a recipient that the channel's behaviour rule maps to a permanent failure, when
dispatch is attempted, then the send fails and is **never** retried (`NG-4`), and the failure is
recorded against the delivery row.

**CC-38.** Given a recipient that the behaviour rule maps to transient failures, when dispatch is
attempted, then the bounded retry policy applies (`NG-3`) with no more than
`NotificationGatewayConstants.TransientRetryCount` attempts, and a subsequent success is recorded.

**CC-39.** Given each channel's outbound adapter, when its payload is built, then it matches that
provider's documented schema field-for-field — asserted directly against the adapter, so the mock
and a real provider cannot drift apart silently.

#### Inbound SMS, provider-faithful (`FEAT-25`, completing `CC-11`–`CC-12`)

**CC-40.** Given a Twilio-shaped inbound SMS webhook with a valid `X-Twilio-Signature`, when it is
received at the SMS inbound endpoint, then the shared ingestion path (`CC-1`–`CC-4`) runs with
`Channel = SMS`. Twilio signs **HMAC-SHA1 over the request URL plus alphabetically-sorted POST
params**, not SHA256 over the raw body as Meta does — which is why
`IWebhookSignatureVerifier.Verify` already carries the `requestUrl` parameter Meta ignores.

**CC-41.** Given an absent or wrong `X-Twilio-Signature`, when the request is received, then it is
refused before any database write and the payload is not logged in full (`CC-27`, `CC-29`).

#### Inbound email (`FEAT-35`, new scope)

**CC-42.** Given a verified inbound email payload in SendGrid Inbound Parse's shape
(`multipart/form-data` carrying `from`, `subject`, `text`, `envelope`), when it is received at the
email inbound endpoint, then the shared ingestion path runs with `Channel = Email`, matching the
customer by email address per `A17`.

**CC-43.** Given the same inbound email delivered twice with the same provider message id, when both
are received, then exactly one `TicketMessage` exists — inheriting the existing unique filtered
index on `(Channel, ProviderMessageId)`.

**CC-44.** Given an agent replies to a ticket whose `Source` is `Email`, when the reply is sent, then
it dispatches through `INotificationGateway` with `Channels=[Email]` and is recorded as an outbound
`TicketMessage`. (Today `RecordTicketMessageCommandHandler:72` dispatches only for `WhatsApp` and
`SMS`; an email-sourced ticket's reply reaches nobody.)

#### Simulators for the two channels with no provider (`FEAT-26`, `FEAT-27`)

**CC-45.** Given the gateway's live-chat visitor simulator, when it runs, then it opens an anonymous
session, exchanges messages over that session's group, and an agent claim → message → close cycle
completes without any authenticated visitor credential (`CC-14`–`CC-17`, `CC-19` unchanged).

**CC-46.** Given a `Waiting` session older than the configured timeout, when the timeout job runs,
then the session is marked `Abandoned` and is not converted to a ticket — closing `CC-18`, which is
specified above but unimplemented.

**CC-47.** Given the gateway's web-form poster, when it submits a valid form, a honeypot-filled form
and a rate-limited burst, then the valid one creates a ticket and returns `201` with the reference
only, and the other two return responses **indistinguishable** from the valid one while creating
nothing (`CC-20`–`CC-23` unchanged; this criterion is that a caller outside the process cannot tell
the defence fired).

> **Revised by the [inbound-completion amendment](#amendment--2026-09-02-inbound-completion)
> below.** "The gateway's web-form poster" assumed a Node script exercising some endpoint; the
> portal-app's own `web-form` feature (built independently, ahead of this criterion, against
> `/api/external/webform/submit`) makes the web form a real screen in the customer portal instead.
> The response contract (`201`, indistinguishable honeypot/rate-limit responses) is unchanged; only
> who the caller is changes. Left in place rather than rewritten, same reasoning as the header note
> at the top of this document.

#### Consolidation this feature requires (`FEAT-35`)

**CC-48.** Given the permitted channel names, when a new channel is added, then exactly one list is
edited. Today there are four divergent copies — `TicketMessage.cs:17` permits seven values,
`RecordTicketMessageCommandValidator.cs:9` six (missing `Portal`),
`IngestInboundChannelMessageCommandValidator.cs:8` three, and `CreateTicketCommandValidator.cs:54`
a sixth set for `Ticket.Source` — and they disagree today. They resolve from one `Domain` source of
truth, and the reconciliation includes `Email` becoming a legal inbound channel. All five names fit
the `Channel` column's 20-character limit.

**CC-49.** Given the three HTTP channel senders, when a fourth provider adapter is added, then the
shared transport concerns are written once. `ApplyAuth` and both `IsTransient` overloads are
currently verbatim copies across `EmailNotificationChannelSender.cs:85-112`,
`SmsNotificationChannelSender.cs:85-112` and `WhatsAppNotificationChannelSender.cs:93-120`; they
move to one base, leaving each adapter owning only its payload shape and its id/error mapping.

**CC-50.** Given the backend test suite, when it runs with the mock gateway not started, then every
test passes — no test depends on port 3001. Integration tests continue to use the in-process
`Integration/StubGatewayServer.cs`.

**CC-51.** Given a gateway configuration whose credential `DatabaseExternalApiProvider` has already
decrypted, when a dispatch is attempted with `AuthType` of `ApiKey`, `Bearer` or `Basic`, then the
credential reaches the transport header intact and the request is actually sent — no second
`Unprotect` is applied to an already-plaintext value, and no exception escapes `SendAsync`. Proven
by `WhatsAppOutboundReplyTests.CC10_WhatsAppReply_RecordsOutboundMessageAndDispatchesToTheGateway`
turning green, and by a unit test that uses a **real** `DataProtection`-backed `ISecretProtector`
rather than the identity stub that hid this (`A19`). Closes `CC-10` and `CC-13`, which cannot pass
without it.

### Design

**The toggle is a decorator, not a branch.** `ServiceCollectionExtensions.cs:68` is the single
registration behind `IExternalApiConfigurationProvider`, and the three HTTP senders plus
`MetaSignatureVerifier.cs:39` read their base URL and credential exclusively through that port. A
`MockRoutingExternalApiConfigurationProvider` wrapping `DatabaseExternalApiProvider` is therefore the
entire mechanism: one new class, one changed registration, and **no change to any sender or handler**.
Configuration keys: `Channels:UseMocks` (bool, default `false`), `Channels:MockBaseUrl` (default
`http://localhost:3001`), `Channels:MockWebhookSecret`.

**Signature verification stays real in mock mode.** Because the verifier reads its secret from the
same configuration the decorator supplies, the mock can sign its outbound webhooks with the same dev
secret, so inbound HMAC verification actually executes — `CC-5`/`CC-27`/`CC-41` are exercised rather
than bypassed. Verification also stops being single-provider: `MetaSignatureVerifier.cs:23` currently
hard-gates `if (provider != "WhatsApp") return false`, and only one implementation is registered, so
per-provider resolution (Meta SHA256-over-raw-body, Twilio SHA1-over-URL-plus-params) replaces it.

**Gateway side.** One model per provider, following the project's own documented recipe: create
`models/{Name}GatewayModel.js`, `register(...)` it in `models/ServiceRegistry.js`, add
`behaviors/{name}-rules.js` for the deterministic failure triggers, and seed `mocks/{group}/*.json`.
Two new `.env` entries read through `config.js` — `CALLBACK_BASE_URL` (where inbound webhooks are
posted) and `WEBHOOK_SECRET` (what they are signed with). The two simulators are scripts under the
existing `scripts/` directory, exposed through `npm run` like the current `test:sms`/`test:email`.

**Failure triggers are deterministic, not random.** The existing `behaviors/*-rules.js` contract is
`check(payload) → {code, message} | null`; the new rules key off reserved recipients so a test can
force a permanent failure or a transient-then-success sequence on demand. `CC-37`/`CC-38` are only
provable end-to-end because of this — the current SMS mock's random status progression could not
support them.

### Out of scope (additions)

- Deploying the gateway anywhere, or adding authentication to it (`A13` — dev-only by design).
- Procuring or verifying any real provider account (`OQ-CC-2` unchanged), and CAPTCHA (`OQ-CC-1`
  unchanged).
- Per-channel mock toggles (`A15`).
- Making the Node gateway a dependency of the automated test suite (`CC-50` is the opposite).
- Replacing the gateway's existing house-shaped `email`/`sms` routes, or migrating whatever else
  consumes them.
- Voice/phone, unchanged from the original out-of-scope list.

## Amendment — 2026-09-02, inbound completion

Plan 1 (above) built the outbound side and the mock/real toggle. This amendment covers what plan 1
explicitly deferred: the inbound half for the three channels with a real or portal-native transport —
`CC-40`–`CC-44` and the revised `CC-47`. `CC-45`/`CC-46` (live-chat visitor simulator, abandoned-session
job) are **deferred to a later pass, by instruction** — nothing below implements them, and nothing
above required them to ship together.

### Assumptions

- **A20.** The web-form's client is the **portal-app itself**, not a gateway simulator. This narrows
  `A16` for web forms specifically: `frontend/projects/portal-app/src/app/features/web-form/` and
  `frontend/projects/common/src/lib/channels/web-form.api.ts` already exist, built ahead of this
  criterion, and already target `POST /api/external/webform/submit` with a
  `{name, email, subject, description, honeypot?}` request and a `{reference, success}` response. The
  backend endpoint is built to that exact, already-fixed contract rather than the other way around.
  `A16`'s reasoning for live chat (no third party, so its artifact is a simulator) is unchanged.
- **A21.** Email inbound carries **no signature verification**. SendGrid's Inbound Parse (unlike its
  separate Event Webhook feature) does not sign the payloads it posts; `CC-42` does not ask for one.
  The email inbound endpoint is anonymous by necessity (`CC-5`'s reasoning) but authenticates nothing
  about the sender beyond what's in the payload itself — consistent with email being spoofable by
  design and out of this spec's threat model to solve.
- **A22.** `IWebhookSignatureVerifier` gets a **second implementation and a composite dispatcher**,
  not a second interface. `MetaSignatureVerifier.cs:23` today hard-gates
  `if (provider != "WhatsApp") return false` and is the only registration. A new
  `TwilioSignatureVerifier` implements the same interface for `provider == "SMS"`
  (HMAC-SHA1 over the request URL plus alphabetically-sorted POST parameters, per Twilio's actual
  scheme — not Meta's raw-body SHA256), and a `CompositeWebhookSignatureVerifier` — the only class
  actually registered for `IWebhookSignatureVerifier` — dispatches to whichever of the two matches
  `provider`. Neither webhook controller's shape changes.
- **A23.** The shared `IngestInboundChannelMessageCommand` gains an **optional `Subject`**, used for
  the new ticket's subject when creating one, falling back to today's
  `"{Channel} — {CustomerName}"` default when absent. `IngestInboundChannelMessageCommandHandler.cs:74`
  synthesizes a subject unconditionally today, which is correct for WhatsApp/SMS (no subject concept)
  but silently discards the web-form's explicit subject field and the email's `Subject:` header if
  left unchanged. Backward compatible: existing WhatsApp/SMS call sites pass nothing and see no
  behaviour change; `IngestInboundChannelMessageTests.cs`'s existing assertions are unaffected.
- **A24.** The web-form's rate limit **fakes success rather than answering `429`**, per `CC-47`'s own
  "indistinguishable" requirement. The platform's existing `AddRateLimiter` middleware
  (`WebApiServiceExtensions.cs:49-98`) always answers a distinguishable `429` on
  `options.RejectionStatusCode`, which would leak exactly the signal `CC-47` says must not leak. The
  web-form endpoint therefore tracks its own per-IP fixed window in the handler (same shape as the
  `"login"` policy: `PermitLimit`/`Window` per source IP) and, on exceeding it, returns the same
  `201` + fake reference the honeypot path already returns — no ticket created, no distinguishable
  status code.
- **A25.** The web form's returned `reference` is fetched by **one extra read**, not by widening
  `IngestInboundChannelMessageCommand`'s response shape. The shared command returns `Response<Guid>`
  (the message id) today, asserted directly by `IngestInboundChannelMessageTests.cs:83,161`;
  `WebFormController` is the only caller that needs a human-readable ticket reference synchronously,
  so it reads `TicketMessage.TicketId` → `Ticket.Reference` after ingesting rather than changing a
  contract three other controllers (one existing, two new in this amendment) would then all have to
  accommodate. **The read goes through MediatR as a dedicated
  `GetTicketReferenceForMessageQuery`**, not a repository call in the controller: every controller in
  both hosts dispatches through `IMediator` and none injects `IRepository<T>`
  (`GetTicketMessagesQueryHandler.cs` is the shape to copy), so a raw repository read in a controller
  would be the only one of its kind in the codebase.
- **A26.** The gateway gains **inbound simulator scripts for SMS and email**, extending `A13`'s
  dev-only surface to the inbound direction the same way plan 1's outbound mocks did for sends. Each
  script builds a provider-shaped payload (Twilio form-encoded fields for SMS, SendGrid Inbound
  Parse's multipart fields for email), signs the SMS one with the shared `WEBHOOK_SECRET` using
  Twilio's real algorithm, and `POST`s to `${CALLBACK_BASE_URL}/api/channels/{sms,email}/webhook` —
  so `CC-40`–`CC-43` are provable end-to-end without a real Twilio or SendGrid account, exactly as
  plan 1's provider mocks did for the outbound side. The web form has no equivalent script (`A20`):
  the portal-app is already the real client.

- **A27 — correcting `CC-44`'s "one-line fix", found while grounding the plan.** The outbound reply
  block at `RecordTicketMessageCommandHandler.cs:72-89` is not channel-agnostic: it loads
  `customer?.Phone`, skips the send when it is blank (logging a warning), and dispatches with
  `PhoneNumber: phone, Email: null`. Adding `or "Email"` to the gate alone would therefore dispatch
  every email reply with a null `Email` and the customer's phone number in `PhoneNumber` — which
  `EmailNotificationChannelSender` cannot deliver. The corrected shape, following
  `RequestOtpCommandHandler.cs:83-92`'s precedent (`Email:` set for the email channel,
  `PhoneNumber:` set for phone channels, never both):
  the block resolves the customer once, then selects the contact by channel — phone for
  `WhatsApp`/`SMS`, `Customer.Email` for `Email` — and dispatches with only the matching field
  populated. **The email branch additionally skips addresses ending `@channel.invalid`**: those are
  the deterministic RFC-2606 placeholders `IngestInboundChannelMessageCommandHandler.cs:115` mints
  for phone-only customers to satisfy `Customer.Email`'s non-nullable contract, and they are not
  deliverable. Skipping logs the same warning shape the missing-phone path already uses, so a
  silently-undeliverable reply is visible in the log rather than recorded as sent.
### Acceptance criteria (interpretive notes; `CC-40`–`CC-44` and `CC-47` are unchanged in substance)

No new `CC-n` ids are added — `CC-40`–`CC-44` and the revised `CC-47` (see the note above `CC-47`)
already state the observable behaviour this amendment implements. What follows resolves how, not
what:

- `CC-40`/`CC-41` are satisfied by a new `SmsWebhookController` at `POST /api/channels/sms/webhook`,
  built to the same shape as `WhatsAppWebhookController.cs` (anonymous only on the action, raw body
  read before model binding, signature checked before any database write), using the composite
  verifier from `A22`.
- `CC-42`/`CC-43` are satisfied by a new `EmailWebhookController` at
  `POST /api/channels/email/webhook`, `[Consumes("multipart/form-data")]`, reading SendGrid Inbound
  Parse's `from`/`subject`/`text`/`envelope` fields. Idempotency (`CC-43`) uses the `Message-ID:` line
  parsed out of the raw `headers` field — Inbound Parse has no separate id field, but forwards the
  original email's headers verbatim.
- `CC-44` — **corrected 2026-09-02 while grounding the plan; see `A27`.** This was written above as
  "a one-line fix" to `RecordTicketMessageCommandHandler.cs:72`'s channel gate. It is not. The block
  it would join (`:72-89`) resolves the customer's **phone** and dispatches with
  `PhoneNumber: phone, Email: null`; email needs the inverse, and needs a placeholder-address guard.
  `A27` carries the corrected design.
- `CC-47` is satisfied by a new `WebFormController` at `POST /api/external/webform/submit`, matching
  `WebFormApi`'s existing request/response contract exactly (`A20`), applying the honeypot check and
  the per-IP rate limit (`A24`) before the real path, and returning the reference via `A25`'s extra
  read.

### Design

**Signature verification becomes a composite.** `TwilioSignatureVerifier` (new,
`Infrastructure/Channels/`) mirrors `MetaSignatureVerifier.cs`'s shape exactly — a pure function of
`(provider, signature, requestUrl, rawBody)` — but reads its secret from `SmsGateway`'s
`Auth.Value` and computes Twilio's `Base64(HMAC-SHA1(url + sorted-and-concatenated POST params))`
instead of Meta's `hex(HMAC-SHA256(rawBody))`. `CompositeWebhookSignatureVerifier` (new) holds both
verifiers and is the only one registered for `IWebhookSignatureVerifier` in
`ServiceCollectionExtensions.cs:107` (replacing the direct `MetaSignatureVerifier` registration).
Neither existing webhook controller nor its tests change shape — they already depend on the
interface, not the concrete class.

**The two new webhook controllers follow `WhatsAppWebhookController.cs` almost verbatim**: no
class-level `[AllowAnonymous]`, buffer and re-read the raw body, verify before deserializing, return
`200` once authenticity is established regardless of downstream outcome (a failed ingestion is not a
retryable webhook), `400`/`401` before any database write otherwise. `SmsWebhookController` reads
`Request.Form` (Twilio posts `application/x-www-form-urlencoded`); `EmailWebhookController` reads
`Request.Form` under `[Consumes("multipart/form-data")]`.

**`WebFormController` composes three concerns in order**: honeypot (any non-empty value in the
honeypot field), then the per-IP fixed-window check (`A24`), then — only if both pass — the real
`IngestInboundChannelMessageCommand` call with `Channel = "WebForm"` and the new `Subject` (`A23`),
followed by the extra reference read (`A25`). The honeypot and rate-limit paths both return
`{reference: "TKT-" + <same digit width the real generator uses>, success: true}` built from a
non-persisted random value — visually identical to a real reference, backed by nothing.

**Gateway-side additions** (`cms-integration-gateway/scripts/`): `simulate-sms-inbound.js` and
`simulate-email-inbound.js`, following the existing `scripts/test-*.js` convention — read
`CALLBACK_BASE_URL`/`WEBHOOK_SECRET` from `config.js` (already added in plan 1's Task 13), build the
provider-shaped payload, sign it where applicable, `POST` it, and print the response. Exposed via
`npm run simulate:sms` / `npm run simulate:email`, matching `test:sms`/`test:email`'s existing
pattern.

### Out of scope (additions, this amendment)

- `CC-45`/`CC-46` (live-chat visitor simulator, abandoned-session job) — deferred to a later pass, by
  instruction. Not touched by anything in this amendment.
- A web-form simulator script (`A20`/`A26`) — the portal-app is the real client; scripting a fake one
  would test nothing the real screen doesn't already exercise.
- CAPTCHA, real Twilio/SendGrid account procurement — unchanged from `OQ-CC-1`/`OQ-CC-2`.
