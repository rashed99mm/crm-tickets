# Task 04 — Delivery Log, Idempotency, and SignalR

**Criteria:** `NG-5`, `NG-6`

## Files

- `Domain/Entities/Notifications/NotificationLog.cs`
- `Infrastructure/Persistence/Configurations/NotificationLogConfiguration.cs`
- `Infrastructure/Notifications/NotificationGateway.cs`
- `Api.Shared/Hubs/NotificationsHub.cs`
- New EF migration and model snapshot.

## Steps

1. Write failing integration tests for duplicate keys, failed delivery logs, and publish-after-save.
2. Add `Pending`, `Sent`, `Failed`, and `Skipped` delivery states.
3. Add a unique index for the approved deduplication key scope.
4. Save in-app rows and logs atomically; publish SignalR only after commit.
5. Review migration `Up` and `Down`; no destructive changes to existing notifications.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~NotificationGateway"`  
**Expected:** One durable delivery exists for a duplicate key and SignalR never precedes persistence.

**Commit:** `feat: add notification delivery tracking`
