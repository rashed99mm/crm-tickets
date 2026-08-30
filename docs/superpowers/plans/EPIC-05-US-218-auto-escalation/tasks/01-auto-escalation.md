# Task 01 - Configure and execute escalation progression

**Story:** `US-218`  
**Criteria:** `AC-218.1`, `AC-218.2`, `AC-218.3` -> original `AC-5.7`  
**Status:** pending; no test execution in this pass

## Files

- Create `backend/src/CustomerSupport.Domain/Entities/Sla/EscalationLevel.cs`.
- Create `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/EscalationLevelConfiguration.cs`.
- Modify `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs`.
- Modify `backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs` and
  `TicketChangeType.cs` for guarded escalation/history.
- Create Application port(s) under `backend/src/CustomerSupport.Application/Interfaces/` for level
  lookup and transition idempotency; ports must expose methods, not EF queryables.
- Modify `backend/src/CustomerSupport.Infrastructure/Jobs/SlaBreachScanner.cs`.
- Create `backend/src/CustomerSupport.Shared.Contracts/Messages/SlaEscalatedMessage.cs` and add its
  topic to `Topics.cs`.
- Create unit tests under `backend/tests/CustomerSupport.Tests/Unit/AutoEscalationTests.cs` and
  integration tests under `Integration/AutoEscalationEndpointTests.cs`.
- Generate/review a migration under `backend/src/CustomerSupport.Infrastructure/Migrations/`.

## Implementation sequence

1. Failing unit tests: `AC2181_FirstBreach_AdvancesToConfiguredLevel`,
   `AC2182_LaterBreaches_AdvanceUntilTerminal`, and
   `AC2183_RepeatingSameBreach_IsIdempotent`.
2. Implement `Ticket.AdvanceEscalation(...)`; reject downward/unknown transitions and append one
   `Escalated` history row with the system actor.
3. Persist level definitions with `Level` uniqueness and positive `BreachMinutes`; enforce terminal
   behavior by absence of a higher active level, not a magic Level3 branch.
4. Refactor scanner processing: record/read the breach, select the next level, claim the unique
   transition, mutate ticket, append history, save in one transaction, then publish one message.
5. Add bounded concurrency retry and duplicate-key no-op handling. Preserve existing
   `ISlaBreachScanner.ScanAsync(...)` return semantics for breach-count callers.

## Tests and evidence

- Unit: `AC2181_TicketAdvanceEscalation_RecordsPreviousAndNextLevel`.
- Unit: `AC2182_EscalationPolicy_StopsAtHighestConfiguredLevel`.
- Unit: `AC2183_EscalationTransition_RejectsDuplicateClaim`.
- Integration: `AC2181_BreachScanner_SetsLevel1AndAppendsHistory`.
- Integration: `AC2182_SecondQualifyingBreach_SetsLevel2AndPublishesRoleTarget`.
- Integration: `AC2182_TerminalLevel_DoesNotCreateFurtherHistory`.
- Integration: `AC2183_ConcurrentScannerRuns_CreateOneTransition`.
- Integration: `AC2183_PendingOrResolvedTicket_DoesNotEscalate`.

```text
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AutoEscalation"
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SlaPauseAndEscalation"
dotnet ef migrations add AddEscalationLevels --project backend/src/CustomerSupport.Infrastructure --startup-project backend/src/CustomerSupport.InternalApi
```

## Authorization and failure behavior

No endpoint is added. System actor identity is server configuration. A stale row version is retried
or treated as an already-applied transition, not exposed as a 500. Any future admin configuration
endpoint must use Admin authorization, `400` field validation, `403` for non-Admin, `404` missing
level, and generic `500 ProblemDetails` for unexpected failures.

## Deviations

None. Do not claim AC evidence until tests and migration review have actually run.
