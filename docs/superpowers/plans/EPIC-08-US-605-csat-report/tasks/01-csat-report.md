# T1 — CSAT data source and report vertical slice

**Story:** `EPIC-08-US-605-csat-report`  
**Acceptance criteria:** `AC-605.1`, `AC-605.2`; negative paths support `AC-608.3` and the shared
report authorization rule.  
**Commit:** pending

## Files

- Create `backend/src/CustomerSupport.Domain/Entities/Reports/CustomerSatisfactionResponse.cs`.
- Create the submission command/handler/validator under
  `backend/src/CustomerSupport.Application/Features/Reports/Commands/SubmitCsat/`.
- Create `CsatReportDto.cs` and query/handler/validator under
  `backend/src/CustomerSupport.Application/Features/Reports/Queries/GetCsatReport/`.
- Modify `backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs`.
- Add integration coverage in `backend/tests/CustomerSupport.Tests/Integration/CsatReportEndpointTests.cs`.
- Add `frontend/projects/common/src/lib/reports/csat-report.api.ts` or extend the existing
  `report.api.ts`, and add the screen under `frontend/projects/admin-app/src/app/features/reports/`.
- Add a migration under `backend/src/CustomerSupport.Infrastructure/Migrations/` only after reviewing
  generated SQL; do not hand-edit an existing migration.

## Implementation steps

1. Write failing tests named `AC6051_Submission_AggregatesByLanguage`,
   `AC6052_Submission_AggregatesByChannel`, `AC605_UnansweredResponse_IsExcludedFromAverageButCountedAsTicket`,
   `AC605_InvalidRange_Returns400KeyedToTo`, `AC148_AgentCannotReadCsat`, and
   `AC605_ResponseDoesNotExposeCustomerIdentity`. Include zero responses and a missing language/channel.
2. Add the immutable response and explicitly bound submission request. Validate rating 1–5, required
   language/channel, ticket existence, and post-resolution eligibility server-side. Do not trust a
   customer id, role, branch, or department sent by the client.
3. Add the query DTO with `byLanguage` and `byChannel` rows containing only grouping key,
   `averageRating`, `totalResponses`, and `totalTickets`. Join/project existing ticket scope and
   response rows; do not return subject, email, description, or message text.
4. Add `GET /api/reports/csat`, `[Authorize(Policy = "Supervisor")]`, UTC `from`/`to` validation,
   and the standard `Response<T>` envelope. Apply the approved US-608 scope before grouping.
5. Add the Angular typed call and a signal-based view using `ReportDateRangeFilter` and the existing
   `AsyncState` loading/empty/error components. Ensure an API error is not rendered as an empty report.
6. Generate and review the migration `AddCustomerSatisfactionResponses`; verify `Down` preserves all
   pre-existing data. Record the actual test output and any deviation here.

## Later verification commands

```text
cd backend && dotnet ef migrations add AddCustomerSatisfactionResponses --project src/CustomerSupport.Infrastructure --startup-project src/CustomerSupport.InternalApi
cd backend && dotnet build CustomerSupport.slnx
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CsatReportEndpointTests"
cd frontend && npx ng test common --watch=false
cd frontend && npx ng test admin-app --watch=false --include="**/csat*spec.ts"
```

## Status / evidence

- **Status:** pending.
- **Test evidence:** not run during planning; paste command output after implementation.
- **Commit:** none.

## Deviations

The current reporting implementation has no CSAT entity or collection mechanism. The shared reporting
design deliberately cut this story, so “add survey response entity if absent” is not an acceptable
shortcut: schema, submission rules, and migration must be approved and tested as part of this task.
