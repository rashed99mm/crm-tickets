# Email channel — outbound provider, agent replies, inbound ingestion

**Feature:** `FEAT-14` (cont.) / `EPIC-10` Integrations · **Epic:** `EPIC-03` Communication channels ·
**Stories:** `US-203`, `US-204`, `US-205`
**Not implemented this pass** — spec and implementation plan only, per explicit instruction.

## Problem

`TicketMessage.Channel` has allowed `"Email"` since `FEAT-14`, but it is a schema value with zero behavior
behind it — `RecordTicketMessageCommandHandler` never branches on `Channel`, and nothing anywhere sends or
receives an actual email. `MailKit` is referenced in `CustomerSupport.Infrastructure.csproj` but never
imported. `NotificationSender` (the platform's one background-delivery worker) marks every notification `Sent`
via a method that is a literal no-op. An agent typing a reply has no way to get it in front of a customer who
isn't watching the portal.

## Assumptions

A1. **Outbound email uses an HTTP transactional-email provider (SendGrid/Mailgun/Postmark-shaped), registered
    through the existing `ExternalApiConfiguration` + `[ExternalApiClient]` Refit pattern — not raw SMTP, and
    not the story's own sketched `EmailProviderConfigurations` table.** Confirmed with you directly rather than
    assumed silently, given it's a real fork: `ExternalApiConfiguration` (`Name`/`BaseUrl`/`AuthType`/
    `AuthValue`/`TimeoutSeconds`/`IsEnabled`) and its provider (`IExternalApiConfigurationProvider`), secret
    handling (`ISecretProtector`), and the reflection-based Refit client scanner
    (`ExternalApiServiceCollectionExtensions`) are already implemented, DI-wired, and proven with two live demo
    clients (`IWeatherClient`, `IPlaceholderClient`) — this is exactly the shape a REST-based email provider's
    API-key auth fits, with zero new configuration tables. `MailKit`'s presence in the `.csproj` with no
    consumer anywhere is flagged as dead weight to remove in this feature's first task, not silently left in
    place or silently adopted.

A2. **Inbound email arrives as a webhook, not a poller**, per the story's own Notes ("an anonymous webhook or
    background poller") and because every mainstream transactional-email provider's inbound-parse feature is
    push-based (SendGrid Inbound Parse, Mailgun Routes) — there is no "list my inbound mail" HTTP endpoint to
    poll against once outbound is provider-based (A1), only a webhook the provider calls when mail arrives.

A3. **The inbound webhook lands on `InternalApi`, not `ExternalApi`, despite being anonymous.** `ADR-0008`
    documents `ExternalApi` as narrow, read-only, anonymous, no seeding — a webhook that creates tickets and
    writes `TicketMessage` rows is a write path a provider calls, not a customer-facing read surface, and
    doesn't fit that host's charter. It is protected the way service-to-service webhooks conventionally are:
    a shared-secret signature header (the provider's own webhook-signing scheme, e.g. SendGrid's
    `X-Twilio-Email-Event-Webhook-Signature`) verified in the action itself, not ASP.NET `[Authorize]` — there
    is no user identity to authenticate against. Recorded as a deliberate exception to `InternalApi`'s
    normally-authenticated surface, the same way `KnowledgeBaseController`'s `/ask` is a deliberate exception on
    the `ExternalApi` side.

A4. **Ticket-reference extraction reads the email subject's `[TKT-nnnnnn]` tag** (matching `US-205`'s AC2 and
    `Ticket.Reference`'s existing format, confirmed against `TicketEndpointTests.cs`'s own fixtures) — no
    `X-Ticket-Id` header dependency, since nothing in this codebase's outbound path would ever set a custom
    header a provider's webhook payload is guaranteed to preserve untouched, whereas the subject line is what
    the provider hands back verbatim in the inbound payload.

A5. **Unknown-sender inbound email creates a new `Customer` record** (the second half of `US-204`'s `BR-20`,
    "or create a new customer") rather than being rejected outright — an inbound email is exactly the same
    trust level as a customer submitting the portal's own ticket form anonymously today, and rejecting it
    silently drops a real customer request on the floor for no security benefit (the webhook signature, not the
    sender address, is the actual trust boundary per A3).

A6. **Retry/backoff (`US-203` AC2, `US-205` AC3, `US-204` AC4) is implemented as an in-process retry inside the
    handler** (three attempts, 1s/2s/4s per the story's own numbers) via `Microsoft.Extensions.Http.Resilience`
    (already a package this codebase's Refit pattern documents using, per `AddStandardResilienceHandler()` in
    the existing `ExternalApiServiceCollectionExtensions`) — not a separate outbox/retry-queue table. A queue
    is real scope this pass doesn't need: `US-205`'s own AC3 says a failed send simply doesn't record a message
    and tells the agent, who can retry by resending — there's no requirement for the system to retry
    unattended after the request returns.

## Out of scope

- CC/BCC recipients (`US-205`'s own Notes: "not required for MVP").
- Dead-letter *review UI* — `US-204` AC4 requires the row exist and be queryable; no admin screen ships this
  pass.
- Attachments on inbound or outbound email.
- Multiple configured providers / provider failover — one active `ExternalApiConfiguration` row for email,
  matching how the existing demo clients each resolve exactly one config by name.

## Acceptance criteria

**Outbound provider (`US-203`)**

AC-196. Given an email provider is configured as an `ExternalApiConfiguration` row (name `"Email"`), when
outbound send is triggered, then the configured provider's API is called with the composed message.

AC-197. Given a transient send failure (5xx, timeout), when send is attempted, then up to 3 retries occur with
exponential backoff (1s, 2s, 4s) via the standard resilience handler (A6), and success on any attempt is
reported as success.

AC-198. Given a non-transient failure (4xx, e.g. invalid recipient), then no retry occurs, the failure is
logged, and the caller receives a documented error — never a silent success.

AC-199. Given no `"Email"` `ExternalApiConfiguration` row exists or it is disabled, when a send is attempted,
then the caller receives a documented not-configured error (matching the `NoOpAiService`/`ERR052` pattern
`FEAT-21` already established for "feature degrades when unconfigured, rather than throwing an unhandled
exception").

**Outbound reply from a ticket (`US-205`)**

AC-200. Given an agent composes and sends a reply on a ticket, when the send succeeds, then a `TicketMessage`
row is created with `Direction = Outbound`, `Channel = Email`.

AC-201. Given the composed email, then its subject contains `[{Ticket.Reference}]` (A4).

AC-202. Given the send fails (transient, after exhausting retries, or non-transient), then no `TicketMessage`
row is created and the agent's request returns the failure — never a message row for an email that was never
actually delivered.

**Inbound ingestion (`US-204`)**

AC-203. Given an inbound webhook payload whose subject contains no recognizable `[TKT-nnnnnn]` tag, when
processed, then a new `Ticket` is created (creating a new `Customer` if the sender is unknown, A5) and the
email body is recorded as the first `TicketMessage` (`Direction = Inbound`, `Channel = Email`).

AC-204. Given a payload whose subject contains a known ticket's reference tag, when processed, then the body
is appended to that ticket as an inbound message — no new ticket is created.

AC-205. Given a payload whose provider-assigned message id has already been processed (an `EmailIngestionLog`
row exists for it), when the webhook fires again for the same id, then no duplicate ticket or message is
created, and the response is still success (idempotent, not an error — a provider's own retry-on-no-200
behavior must not be treated as a failure).

AC-206. Given processing throws a non-transient error (malformed payload, unresolvable ticket state), then no
ticket/message is written, an `EmailIngestionLog` row is created with `Status = Failed` and the error, and it
remains queryable — no silent drop.

## Design

### Backend: Infrastructure

**Remove:** the unused `MailKit` `PackageReference` from `CustomerSupport.Infrastructure.csproj` (A1).

**New:** `IEmailClient` (Refit interface, `[ExternalApiClient("Email")]`, one `POST` method matching the chosen
provider's send-message shape — e.g. SendGrid's `/v3/mail/send`), registered through the existing
`AddExternalApiServices()` scanner exactly like `IWeatherClient` — no new registration code path.
`EmailIngestionLog` entity + `IRepository<EmailIngestionLog>`, matching every other entity's repository
pattern.

### Backend: Application

**New, under `Features/Email/`:** `Commands/SendTicketReplyEmail` (AC-200–202, calls `IEmailClient` then
`RecordTicketMessageCommandHandler`'s existing logic for the actual row write — reused, not duplicated).
`Commands/IngestInboundEmail` (AC-203–206: parses the webhook payload, checks `EmailIngestionLog` for the
provider message id, resolves-or-creates ticket/customer, delegates to the existing
`CreateTicketCommandHandler`/`RecordTicketMessageCommandHandler` for the actual writes rather than
reimplementing ticket/message creation).

### Backend: API

**New:** `EmailWebhookController` (`InternalApi`, `POST /api/email/inbound`), `[AllowAnonymous]` with explicit
signature verification inside the action (A3) — documented in the controller's own XML comment as the reason
`[AllowAnonymous]` is correct here and not a gap, so a future reviewer doesn't "fix" it into requiring a JWT a
webhook provider can never send.

### Data model

One migration: `EmailIngestionLog` table (`Id`, `ExternalMessageId` unique, `FromAddress`, `Subject`,
`TicketId?`, `Status`, `ErrorMessage?`, `ProcessedAt`). No changes to `Ticket`/`TicketMessage` — `Channel =
"Email"` already exists as a valid value (`FEAT-14`); this feature is the first thing to actually set it with
real behavior behind it.

### Error behavior

New codes: `EMAIL_NOT_CONFIGURED` (503, AC-199, mirroring `ERR052`'s pattern), `EMAIL_SEND_FAILED` (502,
AC-198/202). Inbound ingestion failures (AC-206) do not surface as an HTTP error to the *provider* — the
webhook endpoint still returns `200` on a logged, non-transient failure, so the provider doesn't endlessly
retry a payload that will never succeed; the failure is visible only in `EmailIngestionLog`, not in the
response.
