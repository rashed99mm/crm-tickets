# Task 08 - Reports Management Completion

**Status:** Ready  
**Closes gaps:** Export PDF/CSV, dashboard trend percent hardcoded, dashboard CSAT hardcoded, per-agent drill-down.

## Files

- Backend: `ReportsController.cs`, `Features/Reports/**`
- Frontend API: `common/src/lib/reports/report.api.ts`
- Frontend UI: `admin-app/src/app/features/dashboard`, `features/reports/*`

## Implementation

- Add comparison-period DTO fields.
- Add export endpoints for ticket volume, SLA, CSAT, agent performance, audit if backend export is chosen.
- Add agent performance detail endpoint.
- Replace dashboard literals with API values.
- Add export buttons to report pages.

## Code Example

```csharp
public sealed record ReportExportQuery(
    string Report,
    string Format,
    DateOnly? From,
    DateOnly? To) : IRequest<FileResult>;
```

```ts
exportReport(report: string, format: 'csv' | 'pdf', filters: ReportFilters): Observable<Blob> {
  return this.http.get(`/api/Reports/${report}/export`, { params: { ...filters, format }, responseType: 'blob' });
}
```

## Acceptance

- [ ] Each report exports CSV.
- [ ] PDF export exists where required by rubric.
- [ ] Dashboard trend values come from backend comparison.
- [ ] Dashboard CSAT comes from CSAT report endpoint.
- [ ] Agent row drill-down shows real detail.

## Evidence

Pending.
