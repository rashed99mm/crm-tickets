# Task 01 - Select and transactionally assign new tickets

**Story:** `US-220`  
**Criteria:** `AC-220.1`, `AC-220.2`, `AC-220.3` -> original `AC-5.6`  
**Status:** pending; no test execution in this pass

## Files

- Create `backend/src/CustomerSupport.Domain/Entities/Organisation/AssignmentStrategy.cs` and
  `AgentRotation.cs` (or place them under a dedicated `Entities/Assignments` namespace if that is
  the established domain convention before coding).
- Create EF configurations under
  `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/AssignmentStrategyConfiguration.cs`
  and `AgentRotationConfiguration.cs`.
- Modify `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs` and generate a
  migration under `backend/src/CustomerSupport.Infrastructure/Migrations/`.
- Create Application records/ports under
  `backend/src/CustomerSupport.Application/Features/Tickets/Assignment/` and interfaces under
  `Application/Interfaces/`; no EF or Identity types in the Application project.
- Create `AssignmentSelectorTests.cs` and `AutoAssignmentTests.cs` under
  `backend/tests/CustomerSupport.Tests/Unit/`.
- Modify `CreateTicketCommandHandler.cs` to invoke the policy after `Ticket.Create(...)` and before
  the single `SaveChangesAsync(...)`; preserve SLA target calculation.
- Create `Integration/AutoAssignmentTests.cs`; extend `Integration/TicketEndpointTests.cs` only if
  shared helpers are genuinely reusable.

## Implementation sequence

1. Failing unit tests: `AC2202_RoundRobin_SelectsNextPersistedRotation`,
   `AC2203_LoadBased_SelectsLowestActiveCountAndStableTie`, and
   `AC2201_NoEligibleAgent_ReturnsNull`.
2. Implement pure selectors with exact tie rules. Round-robin wraps after the highest sort order;
   load-based orders by active count, then sort order, then AgentId.
3. Add repositories/ports for active Agent candidates, active ticket counts, and rotation claim.
   Candidate query must enforce active Agent role, branch/category scope, and capacity.
4. Integrate into `CreateTicketCommandHandler`: if policy is disabled or selection is null, save the
   unassigned ticket; otherwise call `AssignTo(agentId, systemActorId)` and update rotation inside
   the same `IUnitOfWork` transaction.
5. Add unique constraints and bounded concurrency retry. A second invocation that sees a non-null
   `AssigneeId` is a no-op, never a reassignment. Confirm exactly one `TicketHistory` row.

## Tests and evidence

- Unit: `AC2201_AssignmentPolicy_DisabledLeavesTicketUnassigned`.
- Unit: `AC2201_NoEligibleAgent_LeavesTicketQueued`.
- Unit: `AC2202_RoundRobin_WrapsAndPersistsRotationAcrossRuns`.
- Unit: `AC2203_LoadBased_UsesLeastActiveCount`.
- Unit: `AC2203_LoadBased_UsesAgentIdAsStableTieBreaker`.
- Integration: `AC2201_CreateTicket_AutoAssignsEligibleAgent`.
- Integration: `AC2202_CreateTickets_AdvanceRoundRobinAfterRestartedScope`.
- Integration: `AC2203_CreateTicket_ExcludesInactiveAndOutOfBranchAgents`.
- Integration: `AC2201_ConcurrentCreate_DoesNotOverwriteManualAssignment`.
- Integration: `AC2201_AutoAssignment_WritesOneAssignedHistoryEvent`.

```text
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AutoAssignment"
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketEndpoint"
dotnet ef migrations add AddAssignmentStrategies --project backend/src/CustomerSupport.Infrastructure --startup-project backend/src/CustomerSupport.InternalApi
```

## Authorization and failure behavior

The system actor id comes from configuration and is never accepted in `CreateTicketRequest`. Manual
assignment remains protected by the existing endpoint policy and row-version conflict mapping. A
selection failure is logged with correlation id and leaves the ticket queued; an unexpected database
failure rolls back the transaction and maps to generic `500 ProblemDetails` only at the API boundary.

## Deviations

None. Do not mark the task done until the migration is reviewed and the named AC tests have run with
their actual output pasted here.
