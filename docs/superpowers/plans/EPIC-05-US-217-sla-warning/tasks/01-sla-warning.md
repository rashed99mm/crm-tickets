# Task 01 - Persist and publish SLA pre-breach warnings

**Story:** `US-217`  
**Criteria:** `AC-217.1`, `AC-217.2` -> original `AC-5.9`  
**Status:** pending; no test execution in this pass

## Files

- Modify `backend/src/CustomerSupport.Domain/Entities/Sla/SLAPolicy.cs` only if the approved policy
  contract adds `WarningThresholdPercent` (validate `0 < value < 100`).
- Create `backend/src/CustomerSupport.Domain/Entities/Sla/SLAWarning.cs`.
- Create `backend/src/CustomerSupport.Application/Interfaces/ISlaWarningScanner.cs` and use the
  existing `IDateTimeService`, `IMessagePublisher`, `IRepository<T>`, and `IUnitOfWork` ports.
- Modify `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs` with `DbSet<SLAWarning>`.
- Create `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/SLAWarningConfiguration.cs`.
- Modify `backend/src/CustomerSupport.Infrastructure/Jobs/SlaBreachScanner.cs` and, if necessary,
  `SlaBreachDetector.cs`; preserve `ISlaBreachScanner.ScanAsync(...)` for existing tests.
- Create shared contract `backend/src/CustomerSupport.Shared.Contracts/Messages/SlaWarningMessage.cs`
  and modify `Topics.cs`.
- Create `backend/tests/CustomerSupport.Tests/Unit/SlaWarningTests.cs` and
  `Integration/SlaWarningEndpointTests.cs` (the latter invokes the scanner through DI; no route is
  required).
- Generate and review a migration under
  `backend/src/CustomerSupport.Infrastructure/Migrations/`.

## Implementation sequence

1. Write failing tests named `AC2171_WarningScanner_CrossedThreshold_RecordsOneWarning` and
   `AC2172_WarningScanner_PausedOrResolvedOrRepeatedRun_DoesNotDuplicate`.
2. Add `SLAWarning.Record(...)`, including empty-ticket/type guards and UTC timestamps. Keep it
   append-only if `IAppendOnlyEntity` is used; otherwise make the unique key the idempotency guard.
3. Add policy threshold persistence, explicit decimal/int column type and unique/index definitions.
4. Select active tickets with a due date and calculate remaining business time through the approved
   `IBusinessHoursCalculator`; do not use `DateTime.UtcNow` in the handler/scanner.
5. Insert the warning claim and publish `SlaWarningMessage` with `Version = 1`. A duplicate-key race
   is a successful no-op. A publisher failure must not create another warning claim.
6. Verify inactive status, `PausedAt`, both response/resolution target types, and a second scanner
   instance. Assert no `EscalationState` or `Ticket.Status` mutation.

## Test names and evidence

- Unit: `AC2171_WarningRecord_StoresTargetAndUtcTimes`.
- Unit: `AC2172_WarningScanner_SkipsPausedAndResolvedTickets`.
- Unit: `AC2172_WarningScanner_RepeatedRunIsIdempotent`.
- Integration: `AC2171_Scanner_PublishesOneVersionedWarningMessage`.
- Integration: `AC2172_ConcurrentScannerRuns_CreateOneWarningRow`.
- Integration: `AC2172_UnexpectedFailure_UsesGenericErrorLoggingWithoutPayload`.

Later evidence commands:

```text
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SlaWarning"
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SlaTracking"
dotnet ef migrations add AddSlaWarnings --project backend/src/CustomerSupport.Infrastructure --startup-project backend/src/CustomerSupport.InternalApi
```

## Authorization and failure behavior

No HTTP authorization surface is added. Scanner failures remain observable in hosted-service logs;
the API envelope is unchanged. If a future policy endpoint is changed, non-Admin callers must get
`403`, malformed threshold input `400` with field-keyed validation, missing policy `404`, and a
database conflict `409` through the existing `Response`/middleware mapping.

## Deviations

None recorded. Do not mark this task done or paste passing output until the commands above have run.
