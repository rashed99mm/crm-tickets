# Notification Gateway and Communication Channels

**Epic:** `EPIC-03 Communication Channels`  
**Sprint:** `9 — Notification Gateway and Communication Channels`  
**Feature:** `FEAT-15`  
**Related stories:** `US-201`, `US-202`, `US-203`, `US-204`, `US-205`, `US-219`

## Problem

Notification-producing features currently know too much about delivery. The platform has in-app
notification records and message consumers, but email and SMS consumers do not deliver through a
single policy-controlled gateway. Delivery failures, templates, user preferences, retries, and
provider responses therefore cannot be handled consistently.

## Assumptions

- **A1:** `AppDbContext`, `IRepository<T>`, `IMessageFactory`, `Response<T>`, and the existing
  `ExternalApiConfiguration` abstraction remain the project boundaries.
- **A2:** Email and SMS providers are HTTP integrations configured by the names `EmailGateway` and
  `SmsGateway`; credentials are protected with `ISecretProtector`.
- **A3:** `InApp` is a first-class dispatchable channel delivered over SignalR. The in-app channel is
  served by an `INotificationChannelSender` (like Email/SMS) rather than as a side effect of other
  channels; an in-app `Notification` row is persisted and the payload is pushed over SignalR to the
  recipient's user group. An email/SMS-only request creates no in-app row.
- **A4:** OTP dispatch may bypass ordinary user notification preferences because verification is a
  security-critical delivery; all other dispatches respect preferences.
- **A5:** Expected provider failures are recorded as failed delivery results and logs; raw provider
  responses, credentials, message bodies, and OTP codes are never returned or logged.

## Out of scope

- WhatsApp, live chat, and provider-specific SDKs.
- SMS business workflows beyond the configured SMS integration.
- Autonomous retry scheduling beyond the bounded retry policy in this feature.

## Acceptance criteria

- **NG-1:** Given a valid dispatch request, when the gateway resolves a template and channel, then it
  renders the message and sends through the selected channel adapter.
- **NG-2:** Given `EmailGateway` or `SmsGateway` configuration, when email or SMS is dispatched,
  then the adapter calls the configured integration URL with protected credentials restored only at
  the transport boundary.
- **NG-3:** Given a transient provider timeout or 5xx response, when delivery is attempted, then the
  gateway performs bounded retries and records the final status without leaking provider details.
- **NG-4:** Given a permanent provider or validation failure, when delivery is attempted, then no
  retry occurs and the result is `Failed` with a stable application error code.
- **NG-5:** Given an in-app channel, when dispatch succeeds, then the notification row is persisted
  before any SignalR event is published.
- **NG-6:** Given the same deduplication key twice, when both dispatches run, then only one durable
  delivery is created.
- **NG-7:** Given a missing template, variable, recipient address, or provider configuration, when
  dispatch runs, then the gateway returns a safe failure and does not call the provider.
- **NG-8:** Given an unauthorized caller, when an admin notification-log or retry endpoint is called,
  then the request is refused with the standard forbidden envelope.
- **NG-9:** Given a dispatch request carrying one or more `NotificationChannel` values, when the gateway
  routes delivery, then each channel is dispatched only through its registered `INotificationChannelSender`
  resolved from `INotificationDispatcher`; an unregistered channel yields a safe failed result, not a
  provider call.
- **NG-10:** Given a dispatch request containing `InApp`, when the gateway routes it, then the in-app
  sender persists a `Notification` row (Channel=`InApp`) and publishes the payload over SignalR to the
  recipient's user group, with the durable row persisted before the SignalR push. A request that does
  not list `InApp` creates no in-app row.

## Design

Application owns `INotificationGateway`, `INotificationChannelSender`, dispatch DTOs, and provider
ports. Infrastructure owns HTTP adapters, integration configuration, persistence, retry
classification, and SignalR publication. Feature handlers publish a provider-neutral dispatch
request and never call an email or SMS URL directly.

The existing `NotificationChannel` values remain the channel vocabulary. The current
`EmailMessageConsumer` and `SmsMessageConsumer` become message-bus adapters that delegate to the
gateway instead of simulating delivery with `Task.Delay`.

`INotificationGateway.SendAsync` does not call channel senders directly. It resolves each requested
channel through `INotificationDispatcher`, a registry that maps a `NotificationChannel` to exactly one
`INotificationChannelSender` (`SupportedChannel`). The gateway fans out across `request.Channels`,
dispatches each through its resolved sender, aggregates the `ChannelSendResult` values, and records
them. An unregistered channel produces a safe `Failed` result and never reaches a provider. This keeps
the gateway free of per-channel branching and lets new channels register without touching gateway code.

`InAppNotificationChannelSender` (`SupportedChannel = NotificationChannel.InApp`) is one such sender.
It persists a durable `Notification` row via `INotificationDomainService`/`AppDbContext` and then
publishes the rendered payload over SignalR using `IHubContext<MainHub>` to the group
`user:{recipientUserId}`. It requires `RecipientUserId`; anonymous in-app dispatch is unsupported.

## API and error contract

All HTTP endpoints use the existing `Response<T>` envelope, `IMessageFactory`, trace ID, timestamp,
and `this.ToActionResult(...)` mapping. Add stable domain keys and system-code mappings for missing
configuration, invalid template data, and delivery failure. Provider credentials and body content
must not appear in errors or logs.
