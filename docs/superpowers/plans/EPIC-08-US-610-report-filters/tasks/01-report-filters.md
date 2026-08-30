# T1 — Shared report filter contract and UI

**Story:** `US-610-report-filter-ui`  
**Criteria:** `AC-610.1`, `AC-610.2`, `AC-610.3`, `AC-610.4`  
**Status:** partial, date-range slice exists  
**Commit:** none

## Files

- Add typed request/validation types under `backend/src/CustomerSupport.Application/Features/Reports/`
  and modify all three query/validator files plus
  `backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs`.
- Add `backend/tests/CustomerSupport.Tests/Integration/ReportFilterEndpointTests.cs`.
- Extend `frontend/projects/common/src/lib/reports/report.api.ts` and the shared filter component;
  update `frontend/projects/common/src/public-api.ts`, translations, and each report component/spec.

## Steps

1. Write failing tests `AC6101_DateRange_FiltersTicketVolume`,
   `AC6101_FromAfterTo_Returns400OnTo`, `AC6102_CategoryIds_FilterResults`,
   `AC6103_Priorities_FilterResults`, `AC6104_BranchFilter_UsesServerScope`,
   `AC610_InvalidCategoryGuid_Returns400`, `AC610_InvalidPriority_Returns400`,
   `AC610_PageZeroOrOversize_Returns400`, and `AC610_ClientCannotWidenBranchScope`.
2. Add one reusable predicate/specification. Validate server-side, bound list sizes and paging, apply
   scope first, then date/category/priority predicates. Keep DTO projections and `Response<T>`.
3. Normalize UI dates into UTC wire values, preserve applied filters in query params, and use the same
   names for report/export. Decide repeated versus CSV list syntax before OpenAPI/client generation.
4. Add translated controls only when real category/priority/branch option sources exist. Do not render
   a branch control that sends dead/null data while US-608 is unresolved.
5. Test each screen's apply path for exactly one request, URL back/forward behavior, loading/empty/error
   states, and no leakage. Paste actual command output after execution.

## Later commands

```text
cd backend && dotnet build CustomerSupport.slnx
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportFilterEndpointTests"
cd frontend && npx ng test common --watch=false --include="**/report*spec.ts"
cd frontend && npx ng test admin-app --watch=false --include="**/report*spec.ts"
cd frontend && npx ng build admin-app
```

## Evidence / deviations

No commands run during this rewrite. `ReportDateRangeFilter` and the three report components cover the
narrowed date-only addendum (`AC-163`), but no category/priority/branch request parameters exist.
Branch tests remain blocked until US-608 resolves the absent claim/data issue; a null branch column is
not evidence.
