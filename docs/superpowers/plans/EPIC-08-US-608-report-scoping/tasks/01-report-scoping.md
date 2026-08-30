# T1 — Central report scope policy

**Story:** `EPIC-08-US-608-report-scoping`  
**Criteria:** `AC-608.1`, `AC-608.2`, `AC-608.3`, and export reuse `AC-609.3`  
**Status:** blocked pending role/claim and relationship decision  
**Commit:** none

## Files

- Update `docs/superpowers/specs/EPIC-08-EPIC-08-US-608-report-scoping.md` before code if the decision changes.
- Likely Application port under `backend/src/CustomerSupport.Application/Interfaces/`.
- Implementation in `backend/src/CustomerSupport.Api.Shared/` or Infrastructure, never an
  Application-to-Infrastructure reference.
- Apply to `backend/src/CustomerSupport.Application/Features/Reports/Queries/` and the future export
  query; test in `backend/tests/CustomerSupport.Tests/Integration/ReportScopingEndpointTests.cs`.

## Steps

1. Write failing tests `AC6081_SameScope_ReturnsOnlyPermittedRows`,
   `AC6082_Admin_ReturnsAllRows`, `AC6083_CrossScopeRequest_ReturnsForbidden`,
   `AC6083_QueryParameterCannotWidenScope`, `AC6083_ForgedClaimCannotWidenScope`,
   `AC6083_ExportUsesSameScope`, and `AC608_ReportResponsesContainNoCustomerPii`.
2. Do not proceed until fixtures can carry non-null scope and authenticated clients issue the approved
   claim/relationship. Current fixtures cannot prove this because those fields are unset.
3. Resolve scope server-side from `ICurrentUser`/claims and permitted relationships. Apply the same
   predicate to ticket volume, SLA, agent, CSAT, dashboard, live queue, and export reads before any
   `GroupBy`, `Skip`, `Take`, or file writer. Admin-all must be an explicit authorization branch.
4. Keep unauthorized scope failures as the shared forbidden envelope. Do not return a different 404 or
   leak whether another department has rows. Do not log report rows or request bodies.
5. Review any migration/backfill and paste actual targeted/full test output after execution.

## Later commands

```text
cd backend && dotnet build CustomerSupport.slnx
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportScopingEndpointTests"
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportsEndpointTests"
```

## Evidence / deviations

No build or test was run. Existing `AC148` proves only Admin/Supervisor endpoint authorization. It does
not prove department or branch isolation. If product chooses role-gated reports instead, update the
approved spec and story status rather than marking these tests passed.
