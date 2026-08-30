# FEAT-15 Notification Gateway and Communication Channels — Implementation Plan

**Spec:** `docs/superpowers/specs/EPIC-03-US-219-notification-gateway.md`  
**Epic:** `EPIC-03 Communication Channels`  
**Sprint:** `9`  
**Status:** partial — InApp (SignalR) channel, gateway, dispatcher, template renderer, Email/SMS adapters,
error codes + system codes, and unit tests are implemented and passing. Bus-consumer delegation and
admin/external notification endpoints (Task 5) and the full evidence gate (Task 6) are pending.

## Implementation status (as executed)

- Application contracts (`INotificationGateway`, `INotificationChannelSender`, `INotificationDispatcher`,
  `INotificationTemplateRenderer`, `IRealTimeNotifier`, DTOs) — implemented.
- `INotificationChannelSender` implementations: `InAppNotificationChannelSender` (persists the
  `Notification` row + pushes over SignalR to `user:{userId}`), `EmailNotificationChannelSender`,
  `SmsNotificationChannelSender` (config-driven integration URLs) — implemented.
- `NotificationGateway` fan-out and `NotificationDispatcher` routing — implemented.
- Error codes `NOTIFICATION_*` mapped to `ERR061`–`ERR066` — implemented (no magic strings).
- `NotificationGatewayConstants` for config names / SignalR group prefix / retry policy — implemented.
- Unit tests (`NotificationGatewayTests`) — 4 passing: dispatcher routing, unsupported channel,
  InApp persistence + SignalR push, InApp-without-recipient safe failure.
- Open item: the portal/admin client must call `JoinGroup("user:{userId}")` on `/hubs/main` to receive
  pushes (frontend follow-up under FEAT-15 / EPIC-09).

## Existing code to preserve

- `backend/src/CustomerSupport.Domain/Entities/Notifications/Notification.cs`
- `backend/src/CustomerSupport.Domain/ValueObjects/NotificationChannel.cs`
- `backend/src/CustomerSupport.Infrastructure/Jobs/NotificationSender.cs`
- `backend/src/CustomerSupport.Infrastructure/Messaging/Consumers/EmailMessageConsumer.cs`
- `backend/src/CustomerSupport.Infrastructure/Messaging/Consumers/SmsMessageConsumer.cs`
- `backend/src/CustomerSupport.Application/Interfaces/IExternalApiConfigurationProvider.cs`
- `backend/src/CustomerSupport.Application/Interfaces/ISecretProtector.cs`
- `backend/src/CustomerSupport.Infrastructure/ExternalApis/Providers/DatabaseExternalApiProvider.cs`
- `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs`
- `backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs`

## Contract

```csharp
public sealed record NotificationDispatchRequest(
    string TemplateCode,
    Guid? RecipientUserId,
    IReadOnlyCollection<NotificationChannel> Channels,
    IReadOnlyDictionary<string, string> Variables,
    string? Email,
    string? PhoneNumber,
    bool BypassUserSettings,
    string? DeduplicationKey,
    string? CorrelationId);

public interface INotificationGateway
{
    Task<NotificationDispatchResult> SendAsync(
        NotificationDispatchRequest request,
        CancellationToken ct = default);
}

public interface INotificationChannelSender
{
    NotificationChannel SupportedChannel { get; }
    Task<ChannelSendResult> SendAsync(RenderedNotification notification, CancellationToken ct = default);
}

public interface INotificationDispatcher
{
    IReadOnlyCollection<INotificationChannelSender> Senders { get; }
    INotificationChannelSender GetSender(NotificationChannel channel);
}

public sealed record RenderedNotification(
    Guid? RecipientUserId,
    string? Email,
    string? PhoneNumber,
    string Title,
    string Message,
    string NotificationType,
    NotificationChannel Channel,
    string? Locale);

public sealed record ChannelSendResult(NotificationChannel Channel, bool Succeeded, string? ErrorCode = null);

public sealed record NotificationDispatchResult(
    bool Succeeded,
    IReadOnlyCollection<ChannelSendResult> ChannelResults);

// InApp is a channel sender like any other; it persists the in-app row and pushes over SignalR.
public sealed class InAppNotificationChannelSender : INotificationChannelSender
{
    public NotificationChannel SupportedChannel => NotificationChannel.InApp;
    // ctor receives INotificationDomainService, AppDbContext, IHubContext<MainHub>
    public Task<ChannelSendResult> SendAsync(RenderedNotification notification, CancellationToken ct = default) { ... }
}
```

## Tasks

### Task 1 — Application contracts and error catalogue

**Files:** `Application/Notifications/`, `Application/Errors/ApplicationErrors.cs`,
`Application/Messages/SystemCode.cs`, `SystemCodeMap.cs`, `Api.Shared/Localization/Resources.yaml`.

**Steps:**

1. Add provider-neutral dispatch/result records and `INotificationGateway`.
2. Add `INotificationChannelSender` (with `SupportedChannel`) and `RenderedNotification` contracts.
3. Add `INotificationDispatcher` (`Senders` + `GetSender(NotificationChannel)`) as the only routing
   seam the gateway uses; the gateway must not branch on channel type directly.
3. Add stable domain keys for missing configuration, invalid template, and delivery failure; map
   them to new system codes and localized messages.
4. Keep `IExternalApiConfigurationProvider` and `ISecretProtector` as the only configuration seams.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~NotificationContract"`  
**Expected:** Contract and system-code tests pass with no duplicate codes.  
**Commit:** `feat: add notification gateway application contracts`

### Task 2 — Template rendering and recipient resolution

**Files:** `Application/Notifications/INotificationTemplateRenderer.cs`, existing notification
entities/configurations, `Infrastructure/Notifications/NotificationTemplateRenderer.cs`.

**Steps:**

1. Resolve one active template per `(TemplateCode, NotificationChannel)`.
2. Render `{{Variable}}` placeholders deterministically and reject missing variables before dispatch.
3. Resolve recipient email, phone, and locale from the user context/Identity projection; use explicit
   request overrides only for anonymous public flows.
4. Sanitize the persisted payload snapshot so secrets and OTP values are excluded.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~TemplateRenderer"`  
**Expected:** Known variables render; missing variables fail without a channel call.  
**Commit:** `feat: add notification template rendering`

### Task 3 — Email and SMS integration adapters

**Files:** `Application/Interfaces/IEmailSender.cs`, `ISmsSender.cs`,
`Infrastructure/Notifications/EmailNotificationChannelSender.cs`,
`SmsNotificationChannelSender.cs`, `Infrastructure/ExternalApis/`.

**Interfaces:**

```csharp
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct = default);
}

public interface ISmsSender
{
    Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken ct = default);
}
```

**Steps:**

1. Implement `INotificationChannelSender` with `SupportedChannel = NotificationChannel.Email` (and
   `Sms` respectively); the senders are resolved via `INotificationDispatcher`, not by name.
2. Load `EmailGateway` or `SmsGateway` using `IExternalApiConfigurationProvider`.
3. Build the configured integration URL and apply only the configured auth scheme.
4. Use an `HttpClient` timeout and classify timeout/connection/5xx as transient.
5. Retry only transient failures with a bounded policy; never retry validation or authentication
   failures.
6. Never log request bodies, OTP values, tokens, API keys, or provider response bodies.
7. Implement `InAppNotificationChannelSender` (`SupportedChannel = NotificationChannel.InApp`):
   build the in-app `Notification` via `INotificationDomainService` (Channel=`InApp`,
   `RecipientUserId` from `RenderedNotification`), call `Send()` and persist inside the unit-of-work,
   then push a minimal DTO `{id,title,message,type,createdAt}` over
   `IHubContext<MainHub>.Clients.Group($"user:{recipientUserId}")`. Return `ChannelSendResult`;
   treat a SignalR exception as a failed channel result (no retry of the in-app row beyond the
   bounded retry policy). InApp requires `RecipientUserId`; an anonymous in-app request is a safe
   `Failed` result.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~NotificationChannelSender"`  
**Expected:** Email and SMS use their configured URLs, retry only transient failures, and return safe results.  
**Commit:** `feat: add email and sms notification adapters`

### Task 4 — Gateway persistence, idempotency, and SignalR

**Files:** `Domain/Entities/Notifications/NotificationLog.cs`,
`Infrastructure/Persistence/Configurations/NotificationLogConfiguration.cs`, migration,
`Infrastructure/Notifications/NotificationGateway.cs`, `Api.Shared/Hubs/NotificationsHub.cs`.

**Steps:**

1. Add delivery-log fields for recipient, template, channel, attempt count, provider ID, status,
   correlation ID, and sanitized payload.
2. Add a unique index for non-null deduplication keys and channel/recipient scope.
3. Implement `NotificationGateway.SendAsync` to fan out across `request.Channels`, resolving each
   sender through `INotificationDispatcher.GetSender(channel)`; an unregistered channel produces a
   safe `Failed` result without a provider call. Aggregate `ChannelSendResult` into
   `NotificationDispatchResult`. Register `NotificationDispatcher` (resolving senders from DI) in
   `ServiceCollectionExtensions`. `InAppNotificationChannelSender` is one of the resolved senders.
4. Persist `Notification`/`NotificationLog` state in one unit-of-work boundary.
5. Publish SignalR only after the in-app row is persisted successfully.
6. Add the migration and inspect `Up`/`Down` for non-destructive behavior.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~NotificationGateway"`  
**Expected:** Duplicate dispatch is idempotent; failed delivery is recorded; SignalR follows persistence.  
**Commit:** `feat: persist notification delivery outcomes`

### Task 5 — Bus consumers and notification APIs

**Files:** existing email/SMS consumers, `ServiceCollectionExtensions.cs`, admin and external
notification controllers, notification settings handlers.

**Steps:**

1. Replace `Task.Delay` consumer behavior with delegation to `INotificationGateway` or the channel
   sender; preserve MassTransit message contracts.
2. Register gateway, renderer, channel senders (including `InAppNotificationChannelSender`), named
   HTTP clients, and SignalR services (`IHubContext<MainHub>` is already registered by
   `WebApiServiceExtensions.AddSignalR()`) in DI.
3. Protect admin logs/retry with the permission policy; scope user inbox/settings to the authenticated
   user.
4. Return `Response<T>` through `ToActionResult` with explicit status declarations.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~NotificationEndpoint"`  
**Expected:** Unauthorized users are refused and user endpoints cannot read another user's data.  
**Commit:** `feat: expose authorized notification management`

### Task 6 — Full evidence gate

**Steps:**

1. Run unit tests for rendering, retry classification, secret hygiene, and idempotency.
2. Run integration tests using fake HTTP handlers, not real provider credentials.
3. Inspect logs and response envelopes for body, token, password, and OTP leakage.
4. Add tests proving an InApp-only request creates exactly one in-app `Notification` row and attempts
   a SignalR publish to `user:{recipientUserId}` (fake `IHubContext`), while an Email-only request
   creates zero in-app rows.
5. Update US-201/203/204/205/219 status evidence and `plans/INDEX.md` only from observed output.

**Run:** `dotnet build backend/CustomerSupport.slnx --warnaserror` then
`dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~Notification"`  
**Expected:** Clean build and all focused notification tests pass.  
**Commit:** `feat: complete notification gateway evidence`
