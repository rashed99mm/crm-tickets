# Centralized Notification Gateway - Legacy Architecture Reference

> Canonical spec: [`../superpowers/specs/EPIC-03-US-219-notification-gateway.md`](../superpowers/specs/EPIC-03-US-219-notification-gateway.md)  
> Canonical implementation plan: [`../superpowers/plans/EPIC-03-US-219-notification-gateway/implementation-plan.md`](../superpowers/plans/EPIC-03-US-219-notification-gateway/implementation-plan.md)

This document is retained as the original architecture exploration. Its examples use the former
CCE Platform namespaces and are not executable against the current `CustomerSupport` solution.
The canonical documents adapt the design to the current application boundaries.

## Goal

Create one centralized notification service that acts as the system gateway for all notification delivery:

- In-app notifications
- SignalR real-time notifications
- Email notifications
- SMS notifications

The notification gateway owns template resolution, rendering, user notification settings, delivery logging, and channel dispatch. Email and SMS delivery must go through the existing integration gateway client instead of being called directly from feature handlers.

Existing building blocks:

| Area | Existing File |
|---|---|
| Notification template domain | `src/CCE.Domain/Notifications/NotificationTemplate.cs` |
| User in-app notification domain | `src/CCE.Domain/Notifications/UserNotification.cs` |
| Notification channel enum | `src/CCE.Domain/Notifications/NotificationChannel.cs` |
| Notification status enum | `src/CCE.Domain/Notifications/NotificationStatus.cs` |
| Admin template APIs | `src/CCE.Api.Internal/Endpoints/NotificationTemplateEndpoints.cs` |
| User inbox APIs | `src/CCE.Api.External/Endpoints/NotificationsEndpoints.cs` |
| Integration gateway client | `src/CCE.Integration/Communication/ICommunicationGatewayClient.cs` |
| Gateway email sender | `src/CCE.Infrastructure/Communication/GatewayEmailSender.cs` |

## Architecture Rules

Use the current CCE architecture. Do not add a generic repository or a separate `IUnitOfWork` abstraction.

### Read Pattern

Use `ICceDbContext` queryables in Application query handlers and notification orchestration reads.

Rules:

- Query with `ICceDbContext`.
- Project to DTOs in Application.
- Use `ToListAsyncEither()`, `CountAsyncEither()`, or existing paging helpers when queryables may be in-memory in tests.
- Keep read mapping out of Infrastructure.

Example:

```csharp
var template = await _db.NotificationTemplates
    .Where(t => t.Code == request.TemplateCode)
    .Where(t => t.Channel == channel)
    .Where(t => t.IsActive)
    .FirstOrDefaultAsync(cancellationToken)
    .ConfigureAwait(false);
```

### Write Pattern

Use `ICceDbContext` directly as the unit-of-work boundary.

Rules:

- Add new entities through `_db.Add(entity)`.
- Mutate tracked entities only when fetched by a write repository or by an Infrastructure implementation using the real `CceDbContext`.
- Call `_db.SaveChangesAsync(ct)` once at the end of the operation whenever possible.
- For notification gateway delivery, persist `NotificationLog` state transitions through the same unit of work where possible.
- Do not call `SaveChangesAsync` from every tiny helper unless the helper is intentionally its own transaction boundary.

Target handler/service shape:

```csharp
_db.Add(notificationLog);
_db.Add(userNotification);

await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
```

### Repository / Service Pattern

Keep specific repositories or services only where they already protect aggregate write behavior or hide infrastructure details.

Use:

- `ICceDbContext` for notification reads, projections, and simple inserts.
- Existing user/profile lookup services for recipient email, phone, locale, and role data if the data is not already exposed by `ICceDbContext`.
- Infrastructure channel senders for external effects: email gateway, SMS gateway, SignalR.

Do not make feature handlers call `ICommunicationGatewayClient` directly.

## System Roles

The plan must respect the roles generated from `permissions.yaml`:

| Role | Notification Capability |
|---|---|
| `cce-admin` | Manage templates, view logs, retry failed notifications, send administrative/broadcast notifications where allowed |
| `cce-editor` | Receive workflow notifications; no template/log management unless permission is explicitly added |
| `cce-reviewer` | Receive review/workflow notifications |
| `cce-expert` | Receive expert workflow, community, and content-related notifications |
| `cce-user` | Receive personal, community, and status notifications; manage own settings |
| `Anonymous` | No in-app inbox; may receive email only for public flows such as newsletter or password recovery when explicitly supported |
| State Representative | Usually represented through assignment/scope, not a role constant; receives country-resource and country-profile workflow notifications |

Authorization rules:

- Internal admin notification endpoints require generated permissions.
- User notification settings and inbox endpoints require authenticated external users.
- Anonymous email flows must not create `UserNotification` rows because there is no user inbox.

## Target Model

### Existing: `NotificationTemplate`

Current issue: `Code` is unique, while `Channel` is a property on the template. That prevents one template code from having email, SMS, and in-app variants.

Recommended change:

- Keep one row per `(Code, Channel)`.
- Replace unique index on `Code` with unique index on `(Code, Channel)`.
- Keep `SubjectAr`, `SubjectEn`, `BodyAr`, `BodyEn`, and `VariableSchemaJson`.

Example template rows:

| Code | Channel | Purpose |
|---|---|---|
| `EXPERT_REQUEST_APPROVED` | `Email` | Full email body |
| `EXPERT_REQUEST_APPROVED` | `Sms` | Short SMS text |
| `EXPERT_REQUEST_APPROVED` | `InApp` | In-app inbox text |

### Existing: `UserNotification`

Keep this entity as the in-app inbox row.

Meaning:

- One rendered notification visible to a user.
- Used by `/api/me/notifications`.
- SignalR should push this row after it is persisted.

Do not create a separate `InAppNotification` entity unless the team wants a rename migration later.

### New: `NotificationLog`

Add domain entity:

`src/CCE.Domain/Notifications/NotificationLog.cs`

Purpose:

- Track every attempted delivery per channel.
- Support admin troubleshooting.
- Support retry.
- Store provider response IDs and errors.

Fields:

| Field | Notes |
|---|---|
| `Id` | `Guid` |
| `RecipientUserId` | nullable for anonymous email flows |
| `TemplateCode` | required |
| `TemplateId` | nullable if missing template caused failure |
| `Channel` | email, SMS, in-app, SignalR if added |
| `Status` | pending, sent, failed, skipped |
| `ProviderMessageId` | gateway response ID |
| `Error` | failure reason |
| `AttemptCount` | starts at 0 or 1 |
| `CreatedOn` | clock time |
| `SentOn` | nullable |
| `FailedOn` | nullable |
| `CorrelationId` | from request/current user accessor |
| `PayloadJson` | sanitized variables/snapshot |

Recommended status enum:

```csharp
public enum NotificationDeliveryStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Skipped = 3
}
```

### New: `UserNotificationSettings`

Add domain entity:

`src/CCE.Domain/Notifications/UserNotificationSettings.cs`

Purpose:

- Let users opt in/out by channel and optionally by event code.
- Let the gateway skip disabled channels consistently.

Fields:

| Field | Notes |
|---|---|
| `Id` | `Guid` |
| `UserId` | required |
| `Channel` | required |
| `EventCode` | nullable; null means default for that channel |
| `IsEnabled` | required |
| `UpdatedOn` | clock time |

Phase 1 should avoid quiet hours unless the BRD explicitly requires it. Add later if needed.

## Application Contracts

### `INotificationGateway`

Add:

`src/CCE.Application/Notifications/INotificationGateway.cs`

```csharp
public interface INotificationGateway
{
    Task<NotificationDispatchResult> SendAsync(
        NotificationDispatchRequest request,
        CancellationToken cancellationToken);
}
```

### `NotificationDispatchRequest`

Add:

`src/CCE.Application/Notifications/NotificationDispatchRequest.cs`

Fields:

| Field | Notes |
|---|---|
| `TemplateCode` | required, upper snake case |
| `RecipientUserId` | nullable for anonymous email |
| `Channels` | one or more channels |
| `Variables` | dictionary used by renderer |
| `Locale` | `ar` or `en` |
| `Email` | optional override |
| `PhoneNumber` | optional override |
| `Source` | optional source module name |
| `CorrelationId` | optional |
| `DeduplicationKey` | optional future idempotency |

### `NotificationDispatchResult`

Add:

`src/CCE.Application/Notifications/NotificationDispatchResult.cs`

Fields:

| Field | Notes |
|---|---|
| `TemplateCode` | request code |
| `RecipientUserId` | nullable |
| `Results` | one result per channel |
| `IsSuccess` | true when no required channel failed |

### `NotificationChannelDispatchResult`

Fields:

| Field | Notes |
|---|---|
| `Channel` | target channel |
| `Status` | sent, failed, skipped |
| `NotificationLogId` | related log |
| `UserNotificationId` | for in-app |
| `ProviderMessageId` | for email/SMS |
| `Error` | failure details |

## Channel Senders

Add a small sender abstraction:

`src/CCE.Application/Notifications/INotificationChannelSender.cs`

```csharp
public interface INotificationChannelSender
{
    NotificationChannel Channel { get; }

    Task<ChannelSendResult> SendAsync(
        RenderedNotification notification,
        CancellationToken cancellationToken);
}
```

### Email Sender

Add:

`src/CCE.Infrastructure/Notifications/EmailNotificationChannelSender.cs`

Behavior:

- Calls `ICommunicationGatewayClient.SendEmailAsync`.
- Uses `EmailOptions.FromAddress`.
- Saves gateway response ID into `NotificationLog.ProviderMessageId`.
- Does not use SMTP directly from the notification gateway.

### SMS Sender

Add:

`src/CCE.Infrastructure/Notifications/SmsNotificationChannelSender.cs`

Behavior:

- Calls `ICommunicationGatewayClient.SendSmsAsync`.
- Requires a phone number.
- Skips with a clear log error when no phone number is available.

### In-App Sender

Add:

`src/CCE.Infrastructure/Notifications/InAppNotificationChannelSender.cs`

Behavior:

- Creates `UserNotification.Render(...)`.
- Adds it through `ICceDbContext`.
- Marks it sent after successful persistence.
- Returns the created `UserNotificationId`.

### SignalR Sender

SignalR is real-time transport, not the persistent inbox.

Recommended Phase 1 behavior:

- Persist in-app notification first.
- Push the persisted notification to the connected user through SignalR.
- Do not treat SignalR as a separate `NotificationChannel` unless product requires logs for live delivery independently.

Add:

- `src/CCE.Api.External/Hubs/NotificationsHub.cs`
- `src/CCE.Infrastructure/Notifications/SignalRNotificationPublisher.cs`

Register:

```csharp
builder.Services.AddSignalR();
app.MapHub<NotificationsHub>("/hubs/notifications");
```

Use a user ID provider so SignalR can route by `UserId`.

## Notification Gateway Implementation

Add:

`src/CCE.Infrastructure/Notifications/NotificationGateway.cs`

Dependencies:

- `ICceDbContext`
- `ISystemClock`
- `ICurrentUserAccessor`
- `IEnumerable<INotificationChannelSender>`
- recipient lookup service if needed
- logger

Flow:

1. Validate request.
2. Normalize channels.
3. Resolve recipient data:
   - user ID
   - email
   - phone
   - locale
   - role/scope only if needed for targeting
4. Load active template for each `(TemplateCode, Channel)`.
5. Check `UserNotificationSettings`.
6. Render subject/body using variables.
7. Create `NotificationLog` as `Pending`.
8. Dispatch through the matching channel sender.
9. Mark log `Sent`, `Failed`, or `Skipped`.
10. Call `_db.SaveChangesAsync(ct)` as the unit-of-work boundary.
11. Publish SignalR update after in-app row is persisted.
12. Return `NotificationDispatchResult`.

Important:

- The gateway should not throw for expected delivery failures. It should return failed channel results and write `NotificationLog`.
- Throw only for programming/configuration errors that should fail fast.
- Avoid logging sensitive variable values in `PayloadJson`.

## Template Rendering

Add:

`src/CCE.Application/Notifications/INotificationTemplateRenderer.cs`

Simple Phase 1 syntax:

```text
Hello {{UserName}}, your request {{RequestNumber}} was approved.
```

Rules:

- Missing variables should fail validation before sending.
- Variable schema stays JSON for now.
- Renderer should be deterministic and unit tested.
- HTML encoding decision belongs to the email sender or renderer; do not double encode.

## API Changes

### Internal Admin APIs

Existing:

- `GET /api/admin/notification-templates`
- `GET /api/admin/notification-templates/{id}`
- `POST /api/admin/notification-templates`
- `PUT /api/admin/notification-templates/{id}`

Add:

| Endpoint | Role / Permission | Purpose |
|---|---|---|
| `GET /api/admin/notification-logs` | `cce-admin` with notification manage permission | List logs |
| `GET /api/admin/notification-logs/{id}` | same | View log details |
| `POST /api/admin/notification-logs/{id}/retry` | same | Retry failed delivery |
| `POST /api/admin/notifications/send` | optional, admin only | Send manual/admin notification |

Permission recommendation:

- Reuse `Permissions.Notification_TemplateManage` for templates.
- Add `Permissions.Notification_LogView` and `Permissions.Notification_Send` only if permission granularity is needed.
- If adding permissions, edit `permissions.yaml` and rebuild `CCE.Domain`.

### External User APIs

Existing:

- `GET /api/me/notifications`
- `GET /api/me/notifications/unread-count`
- `POST /api/me/notifications/{id}/mark-read`
- `POST /api/me/notifications/mark-all-read`

Add:

| Endpoint | Role | Purpose |
|---|---|---|
| `GET /api/me/notification-settings` | authenticated user | Read own settings |
| `PUT /api/me/notification-settings` | authenticated user | Update own settings |

## Domain Event Integration

Use existing domain events and MediatR handlers. Feature handlers should not know about email/SMS/SignalR.

Add notification handlers for existing events:

| Event | Suggested Template Code | Recipients |
|---|---|---|
| `ExpertRegistrationApprovedEvent` | `EXPERT_REQUEST_APPROVED` | requesting user |
| `ExpertRegistrationRejectedEvent` | `EXPERT_REQUEST_REJECTED` | requesting user |
| `CountryResourceRequestApprovedEvent` | `COUNTRY_RESOURCE_APPROVED` | state representative |
| `CountryResourceRequestRejectedEvent` | `COUNTRY_RESOURCE_REJECTED` | state representative |
| `NewsPublishedEvent` | `NEWS_PUBLISHED` | interested users/admin-configured audience |
| `ResourcePublishedEvent` | `RESOURCE_PUBLISHED` | interested users/admin-configured audience |
| `EventScheduledEvent` | `EVENT_SCHEDULED` | interested users |
| `PostCreatedEvent` | `COMMUNITY_POST_CREATED` | topic followers |

Handler pattern:

```csharp
public sealed class ExpertRegistrationApprovedNotificationHandler
    : INotificationHandler<ExpertRegistrationApprovedEvent>
{
    private readonly INotificationGateway _notifications;

    public async Task Handle(
        ExpertRegistrationApprovedEvent notification,
        CancellationToken cancellationToken)
    {
        await _notifications.SendAsync(new NotificationDispatchRequest(
            TemplateCode: "EXPERT_REQUEST_APPROVED",
            RecipientUserId: notification.UserId,
            Channels: [NotificationChannel.InApp, NotificationChannel.Email],
            Variables: new Dictionary<string, string>
            {
                ["UserName"] = notification.FullName
            },
            Locale: "en"), cancellationToken).ConfigureAwait(false);
    }
}
```

## Persistence Changes

### `CceDbContext`

Add DbSets:

```csharp
public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
public DbSet<UserNotificationSettings> UserNotificationSettings => Set<UserNotificationSettings>();
```

Add explicit `ICceDbContext` queryables:

```csharp
IQueryable<NotificationLog> ICceDbContext.NotificationLogs => NotificationLogs.AsNoTracking();
IQueryable<UserNotificationSettings> ICceDbContext.UserNotificationSettings => UserNotificationSettings.AsNoTracking();
```

### `ICceDbContext`

Add:

```csharp
IQueryable<NotificationLog> NotificationLogs { get; }
IQueryable<UserNotificationSettings> UserNotificationSettings { get; }
```

### EF Configurations

Add:

- `NotificationLogConfiguration`
- `UserNotificationSettingsConfiguration`

Indexes:

| Entity | Index |
|---|---|
| `NotificationTemplate` | unique `(Code, Channel)` |
| `NotificationLog` | `(RecipientUserId, Status, CreatedOn)` |
| `NotificationLog` | `(TemplateCode, Channel)` |
| `NotificationLog` | `CorrelationId` |
| `UserNotificationSettings` | unique `(UserId, Channel, EventCode)` |

Migration:

```bash
dotnet ef migrations add AddNotificationGateway --project src/CCE.Infrastructure --startup-project src/CCE.Infrastructure
```

## Dependency Injection

Update:

`src/CCE.Infrastructure/DependencyInjection.cs`

Register:

```csharp
services.AddScoped<INotificationGateway, NotificationGateway>();
services.AddScoped<INotificationTemplateRenderer, NotificationTemplateRenderer>();
services.AddScoped<INotificationChannelSender, EmailNotificationChannelSender>();
services.AddScoped<INotificationChannelSender, SmsNotificationChannelSender>();
services.AddScoped<INotificationChannelSender, InAppNotificationChannelSender>();
services.AddScoped<ISignalRNotificationPublisher, SignalRNotificationPublisher>();
```

Keep:

```csharp
services.AddExternalApiClient<ICommunicationGatewayClient>("CommunicationGateway");
```

## Implementation Phases

### Phase 1 - Data Model and Contracts

- [ ] Add `NotificationLog`.
- [ ] Add `UserNotificationSettings`.
- [ ] Add delivery status enum.
- [ ] Update `NotificationTemplate` unique index to `(Code, Channel)`.
- [ ] Extend `ICceDbContext`.
- [ ] Extend `CceDbContext`.
- [ ] Add EF configurations.
- [ ] Add migration.
- [ ] Add application request/result contracts.

### Phase 2 - Rendering and Settings

- [ ] Add template renderer.
- [ ] Validate variables against `VariableSchemaJson`.
- [ ] Add user settings query.
- [ ] Add user settings update command.
- [ ] Add external settings endpoints.
- [ ] Add tests for settings and rendering.

### Phase 3 - Channel Senders

- [ ] Add email channel sender using `ICommunicationGatewayClient.SendEmailAsync`.
- [ ] Add SMS channel sender using `ICommunicationGatewayClient.SendSmsAsync`.
- [ ] Add in-app channel sender using `UserNotification`.
- [ ] Add channel sender tests with mocked gateway client.

### Phase 4 - Central Gateway

- [ ] Add `NotificationGateway`.
- [ ] Implement template lookup.
- [ ] Implement settings check.
- [ ] Implement log creation and status transitions.
- [ ] Dispatch per channel.
- [ ] Save via `_db.SaveChangesAsync(ct)` as the unit-of-work boundary.
- [ ] Return per-channel result.
- [ ] Add gateway unit tests.

### Phase 5 - SignalR

- [ ] Add `NotificationsHub`.
- [ ] Configure `AddSignalR`.
- [ ] Map `/hubs/notifications`.
- [ ] Add user ID provider if current claims do not map correctly.
- [ ] Publish SignalR event after in-app notification persistence.
- [ ] Add integration test for hub authentication if practical.

### Phase 6 - Admin Logs and Retry

- [ ] Add log list query.
- [ ] Add log details query.
- [ ] Add retry command.
- [ ] Add internal admin endpoints.
- [ ] Add permissions if needed.
- [ ] Add integration tests.

### Phase 7 - Domain Event Handlers

- [ ] Add expert workflow notification handlers.
- [ ] Add country resource request notification handlers.
- [ ] Add content publishing notification handlers.
- [ ] Add community notification handlers.
- [ ] Seed required notification templates.
- [ ] Add tests for handlers calling `INotificationGateway`.

## Testing Plan

### Domain Tests

- [ ] `NotificationLog` starts pending.
- [ ] `NotificationLog` can mark sent.
- [ ] `NotificationLog` can mark failed.
- [ ] `UserNotificationSettings` validates user/channel.
- [ ] `NotificationTemplate` allows same code across different channels.
- [ ] `NotificationTemplate` rejects duplicate `(Code, Channel)`.

### Application Tests

- [ ] Renderer replaces variables.
- [ ] Renderer fails on missing required variable.
- [ ] Settings query returns defaults when user has no explicit settings.
- [ ] Settings update writes expected channel settings.
- [ ] Gateway skips disabled channel.
- [ ] Gateway fails missing template per channel.
- [ ] Gateway returns result per channel.

### Infrastructure Tests

- [ ] Email sender calls integration gateway email endpoint.
- [ ] SMS sender calls integration gateway SMS endpoint.
- [ ] In-app sender creates `UserNotification`.
- [ ] Gateway creates `NotificationLog` rows.
- [ ] Failed gateway response marks log failed.

### API Integration Tests

- [ ] User can read own notification settings.
- [ ] User can update own notification settings.
- [ ] Admin can list notification logs.
- [ ] Admin can retry failed notification.
- [ ] Non-admin cannot access log endpoints.
- [ ] Existing inbox endpoints still pass.

## Build and Verification

Run focused tests while building the slice:

```bash
dotnet test tests/CCE.Domain.Tests --filter "FullyQualifiedName~Notifications"
dotnet test tests/CCE.Application.Tests --filter "FullyQualifiedName~Notifications"
dotnet test tests/CCE.Api.IntegrationTests --filter "FullyQualifiedName~Notifications"
```

Before merge:

```bash
dotnet build CCE.sln
dotnet test CCE.sln
```

Warnings are errors in this solution, so the plan is complete only when the full build is warning-free.

## Rollout Notes

Phase 1 should keep existing notification APIs working.

Recommended rollout:

1. Add database objects and gateway contracts.
2. Add gateway and senders behind tests.
3. Seed templates for one workflow.
4. Move one workflow to the centralized gateway.
5. Verify logs and delivery.
6. Move remaining workflows.
7. Add admin retry and operational dashboards.

## Open Decisions

| Decision | Recommendation |
|---|---|
| Is SignalR a separate channel? | No for Phase 1. Treat it as live transport for in-app notifications. |
| Do anonymous users get logs? | Yes for email/SMS, with `RecipientUserId = null`. |
| Do we need notification audience groups? | Later. Start with explicit recipients from domain event handlers. |
| Do we need background retries? | Later. Start with admin retry endpoint and failed logs. |
| Do we need quiet hours? | Later unless BRD requires it now. |
