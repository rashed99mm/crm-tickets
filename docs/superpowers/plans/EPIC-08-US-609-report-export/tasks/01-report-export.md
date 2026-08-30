# T1 — Bounded CSV/XLSX report export

**Story:** `US-609-export-report`  
**Criteria:** `AC-609.1`, `AC-609.2`, `AC-609.3`  
**Status:** pending  
**Commit:** none

## Files

- Add an Application export contract under `backend/src/CustomerSupport.Application/Interfaces/`.
- Add writers under `backend/src/CustomerSupport.Infrastructure/` using an approved package.
- Modify `backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs`.
- Add `backend/tests/CustomerSupport.Tests/Integration/ReportExportEndpointTests.cs`.
- Extend `frontend/projects/common/src/lib/reports/report.api.ts` and report templates with format
  actions; do not add a client-side serializer as the server contract.

## Steps

1. Write failing tests named `AC6091_TicketVolumeCsv_HasSafeHeadersAndRows`,
   `AC6092_TicketVolumeXlsx_IsReadableAndHasRows`, `AC6093_ExportAppliesServerScope`,
   `AC6093_ExportCannotUseForgedScope`, `AC609_UnsupportedFormat_Returns400`,
   `AC609_AnonymousExport_IsUnauthorized`, `AC609_ExportRejectsReversedDateRange`,
   `AC609_ExportEscapesCsvFormulaAndDelimiters`, and `AC609_ExportRejectsUnboundedRange`.
2. Allow-list report keys and map them to existing projected queries. Pass exactly the same UTC range,
   groupBy, category/priority filters, and resolved scope as the visible report. Never accept raw SQL,
   arbitrary columns, an admin flag, or a client scope override.
3. Implement CSV quoting and XLSX generation behind one writer interface. Bound rows/date range, avoid
   logging contents, set safe ASCII filenames, and return correct MIME/disposition headers.
4. Add the endpoint with Supervisor policy and explicit binary success plus standard 400/401/403
   responses. Add Angular download actions with loading/error state and preserved URL filter state.
5. Review package licensing and generated output, then paste real targeted and full suite evidence.

## Later commands

```text
cd backend && dotnet build CustomerSupport.slnx
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportExportEndpointTests"
cd frontend && npx ng test common --watch=false
cd frontend && npx ng test admin-app --watch=false --include="**/report*spec.ts"
cd frontend && npx ng build admin-app
```

## Evidence / deviations

No commands run during this rewrite. Current `ReportsController`, `ReportsApi`, and report templates
have no export contract. US-608 and US-610 are prerequisites for proving “same scoped/filtered data”;
if either remains unresolved, record the blocked criterion instead of weakening the test.
