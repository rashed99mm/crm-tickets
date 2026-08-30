# Task 05 — Notification APIs and Consumers

**Criteria:** `NG-8`

## Files

- `Infrastructure/Messaging/Consumers/EmailMessageConsumer.cs`
- `Infrastructure/Messaging/Consumers/SmsMessageConsumer.cs`
- Internal admin notification-log controller/handlers.
- External user notification settings handlers.
- `Infrastructure/ServiceCollectionExtensions.cs`.

## Steps

1. Write failing API tests for unauthorized admin access and cross-user inbox access.
2. Delegate bus consumers to the gateway; remove simulated `Task.Delay` delivery.
3. Register all ports/adapters and named HTTP clients in DI.
4. Use `Response<T>`, `IMessageFactory`, and `ToActionResult` for all failures.
5. Apply permission policy server-side; UI visibility is not authorization.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~NotificationEndpoint"`  
**Expected:** Admin-only operations return the standard forbidden envelope for unauthorized users.

**Commit:** `feat: authorize notification management APIs`
