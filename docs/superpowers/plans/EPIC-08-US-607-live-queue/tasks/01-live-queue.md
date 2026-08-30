# T1 — Live queue projection and screen

**Story:** `EPIC-08-US-607-live-queue-dashboard`  
**Criteria:** `AC-607.1`, `AC-607.2`, `AC-607.3`  
**Status:** pending  
**Commit:** none

## Files

- Create DTO/query/validator/handler under
  `backend/src/CustomerSupport.Application/Features/Reports/Queries/GetLiveQueue/`.
- Modify `backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs`.
- Add `backend/tests/CustomerSupport.Tests/Integration/LiveQueueEndpointTests.cs`.
- Add typed methods to `frontend/projects/common/src/lib/reports/report.api.ts` and a view under
  `frontend/projects/admin-app/src/app/features/reports/` or the approved command-center feature.

## Steps

1. Write failing tests named `AC6071_UnassignedActiveTickets_AreOldestFirst`,
   `AC6072_AssignedOpenTickets_AreCountedPerApplicationUser`,
   `AC6073_WaitBeyondThreshold_IsUrgent`, `AC607_InvalidPageSize_IsRejected`,
   `AC607_AgentCannotReadLiveQueue`, `AC607_AnonymousCannotReadLiveQueue`, and
   `AC607_LiveQueueDoesNotExposeCustomerData`.
2. Project `Ticket.Id`, `Reference`, `Subject`, `Priority`, `CreatedAt`, and `AssigneeId`; filter
   `AssigneeId == null` and non-terminal status for queue rows. Compute `waitMinutes` in UTC and use an
   injected clock where the existing application provides one. Query agent loads separately or as one
   bounded projection, resolving `ApplicationUser.FullName` without N+1 calls.
3. Validate `waitThresholdMinutes >= 0`, `page >= 1`, and a documented maximum page size. Apply US-608
   scope before paging and aggregation, and return the standard envelope.
4. Add Angular loading/loaded/empty/error/stale states, threshold highlighting, agent load table, and
   cancellable refresh. Route and nav protection must match `roleGuard('Supervisor', 'Admin')` and the
   backend policy; hiding a link is not authorization.
5. Paste actual test/build output and list every deviation.

## Later commands

```text
cd backend && dotnet build CustomerSupport.slnx
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~LiveQueueEndpointTests"
cd frontend && npx ng test admin-app --watch=false --include="**/live-queue*spec.ts"
cd frontend && npx ng build admin-app
```

## Evidence / deviations

No commands were run for this documentation change. The current code has no live queue endpoint or
component, and the source story's `Agents`/`assignedAgentId` SQL is not an implementation contract for
this schema. Confirm the active status set and scope source before coding.
