# Task 01 - Route SLA events into durable notifications

**Story:** `US-219`  
**Criteria:** `AC-219.1`, `AC-219.2`, `AC-219.3` -> original `AC-5.8`  
**Status:** pending; no test execution in this pass

## Files

- Create `backend/src/CustomerSupport.Application/Interfaces/ISlaNotificationRecipientResolver.cs`.
- Create Application SLA notification handler/consumer files under
  `backend/src/CustomerSupport.Application/Features/Notifications/Sla/`.
- Create versioned contracts under
  `backend/src/CustomerSupport.Shared.Contracts/Messages/` and modify `Topics.cs`.
- Modify `backend/src/CustomerSupport.Domain/Entities/Notifications/Notification.cs` and
  `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs`.
- Modify `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs` only if a separate
  idempotency entity/table is selected.
- Modify `backend/src/CustomerSupport.Infrastructure/Messaging/Consumers/NotificationMessageConsumer.cs`
  and registration; preserve `NotificationSender` as the durable delivery loop.
- Create `backend/tests/CustomerSupport.Tests/Unit/SlaNotificationTests.cs` and
  `Integration/SlaNotificationTests.cs`; add a publisher spy/failure double at the Application seam.
- Generate/review a migration under `backend/src/CustomerSupport.Infrastructure/Migrations/`.

## Implementation sequence

1. Failing tests: `AC2191_BreachFactory_IncludesTicketDetails`,
   `AC2192_WarningFactory_UsesWarningTemplateAndRemainingMinutes`, and
   `AC2193_FailedDelivery_StopsAfterThreeAttempts`.
2. Implement recipient resolution from ticket assignee, their supervisor relationship, and the
   configured escalation target; remove inactive/duplicate recipients.
3. Add the unique idempotency key and create one `Notification` per recipient. Duplicate insertion
   is a no-op; it must not reset `RetryCount` or create another row.
4. Add versioned message contracts and consumers. Publish only after the durable row/claim is saved;
   do not log payload contents.
5. Make provider failure observable and bounded through the existing `Notification` lifecycle and
   `NotificationSender`; NoOp is allowed only when an explicit development/test option is true.

## Tests and evidence

- Unit: `AC2191_BreachMessage_ContainsTicketIdSubjectAndBreachTime`.
- Unit: `AC2192_WarningMessage_UsesWarningTemplateAndRemainingTime`.
- Unit: `AC2191_RecipientResolver_ReturnsAssigneeSupervisorAndEscalationTargetOnce`.
- Integration: `AC2191_BreachEvent_CreatesNotificationsForAssigneeAndSupervisor`.
- Integration: `AC2192_WarningEvent_CreatesOneWarningPerRecipient`.
- Integration: `AC2192_ReplayedWarningEvent_DoesNotDuplicateNotifications`.
- Integration: `AC2193_PublisherFailure_RetriesThreeTimesThenRemainsFailed`.
- Integration: `AC2193_NotificationLogs_DoNotContainMessageBodyOrCredentials`.

```text
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SlaNotifications"
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~Notification"
dotnet ef migrations add AddSlaNotificationIdempotency --project backend/src/CustomerSupport.Infrastructure --startup-project backend/src/CustomerSupport.InternalApi
```

## Authorization and failure behavior

Message handlers are system-authorized. Existing notification queries must continue to scope rows to
the authenticated `IUserContext.UserId`; no recipient may read another user's notification. Delivery
failure is not an HTTP error. Any exposed configuration failure uses the existing response envelope:
`400` validation, `401` unauthenticated, `403` forbidden, and generic `500 ProblemDetails`.

## Deviations

None. The exact provider and whether the message consumer creates or only forwards rows must be
settled before implementation; external delivery cannot be inferred from the current stub consumer.
