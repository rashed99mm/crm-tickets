# Task 01 — Management Dashboard

**Story:** US-606  
**Criteria:** AC-606

1. Write failing API tests for every summary card and authorization scope.
2. Add the query/DTO using shared report scope and filters.
3. Add the Angular route, cards, loading, empty and error states.
4. Add component tests naming each card criterion.
5. Run affected suites and both builds; record output.
# T1 — Management dashboard vertical slice

**Story:** `EPIC-08-US-606-management-dashboard`  
**Criteria:** `AC-606.1`, `AC-606.2`  
**Status:** pending  
**Commit:** none

## Files

- Create `backend/src/CustomerSupport.Application/Features/Reports/Dtos/ManagementDashboardDto.cs`.
- Create query/handler/validator under
  `backend/src/CustomerSupport.Application/Features/Reports/Queries/GetManagementDashboard/`.
- Modify `backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs`.
- Add `backend/tests/CustomerSupport.Tests/Integration/ManagementDashboardEndpointTests.cs`.
- Modify or create the management dashboard view under
  `frontend/projects/admin-app/src/app/features/dashboard/`; reuse `CsCard`, `AsyncState`, and
  `ReportsApi` patterns. Modify routes/nav only if the approved UX gives this a distinct route.

## Steps

1. Write failing tests `AC6061_Dashboard_ReturnsOpenWaitSlaAndCsat`,
   `AC6061_DashboardComputesMetricsServerSide`, `AC6062_AgentIsForbidden`,
   `AC6062_AnonymousIsUnauthorized`, `AC6062_RejectedScopeReturnsNoData`, and
   `AC606_DashboardOmitsCustomerPii`. Seed deterministic tickets, SLA outcomes, and CSAT responses.
2. Implement a single query that applies date UTC bounds and the centralized US-608 scope, then
   projects `openTickets`, `averageWaitMinutes`, `slaAttainmentPercent`, and nullable `averageCsat`.
   Do not call existing report HTTP endpoints from the handler or sum values in Angular.
3. Add `GET /api/reports/dashboard`, the existing Supervisor policy, `Response<T>` envelope, and
   field-keyed 400 for reversed dates. Return 403/401 through the existing authorization pipeline.
4. Add the four cards and explicit loading, empty, and error states. The client sends only UTC range
   values; it cannot submit an admin flag or scope override. Add translated labels using the existing
   `TranslatePipe`/dictionary convention.
5. Record actual test output and deviations. Do not mark the story shipped if US-605/US-608 remain
   unavailable.

## Later commands

```text
cd backend && dotnet build CustomerSupport.slnx
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ManagementDashboardEndpointTests"
cd frontend && npx ng test admin-app --watch=false --include="**/dashboard*spec.ts"
cd frontend && npx ng build admin-app
```

## Evidence

No commands run while rewriting. Paste the real summaries here after implementation.

## Deviations

Current `dashboard.component.ts` is not evidence for this contract, and no `/api/reports/dashboard`
exists. The prior pass deliberately substituted US-602/603/604 screens; that adaptation must remain
visible until this task supplies the four authoritative metrics.
