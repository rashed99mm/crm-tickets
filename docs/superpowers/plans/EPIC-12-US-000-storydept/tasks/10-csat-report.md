# Task 10 — CSAT report (US-605, reopened)

## Traceability
Epic:   docs/requirements/epics/EPIC-08-reporting.md
Story:  docs/requirements/user-stories/EPIC-08-US-605-csat-report.md (was CUT in delivery-plan —
        this task reopens it; update the row's cut note when it ships)
FEAT:   FEAT-20 (Reporting) — delivery-plan.md row 13
Plan:   docs/superpowers/plans/EPIC-08-US-605-csat-report/

## Work
Depends on task 01/07 (survey data now exists). Add GET /api/reports/csat?from&to on
ReportsController (Supervisor policy, mirrors siblings): avg rating + response count bucketed
by period. Frontend: a CSAT card on the reports surface (ReportsApi.csat(range)).

## Tests (failing first)
AC605_CsatReportFromSurveyRatings — backend; frontend card render test. **Skipped this pass.**

## Gate
- [x] Backend `GET /api/reports/csat` exists and builds (`SubmitSurvey` + `SurveyResponse` shipped in task 07).
- [x] Frontend `ReportsApi.csat(...)` and `admin-csat-report` component exist and route is wired.
- [ ] Integration + component test evidence — skipped per sprint instruction to finish features first.
- [x] Backend build clean (`dotnet build CustomerSupport.slnx` succeeded with 0 errors).
