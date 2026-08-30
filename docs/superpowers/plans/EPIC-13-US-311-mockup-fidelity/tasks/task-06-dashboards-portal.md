# Task 06 · Adapt dashboards, analytics and portal

**Criteria:** `AC-406`, `AC-411`, `AC-412`, `AC-416`, `AC-417`, `AC-418`  
**Status:** Completed (All admin/portal dashboard and report specs passed)

## Changes

Adapt `agent_dashboard_overview`, `admin_dashboard`, `management_analytics_sla_performance`,
`user_dashboard` and `user_profile_settings`. Use existing report, ticket and notification state;
where a chart, avatar or metric has no source, preserve its visual region with an explicit
unavailable state.

## Test-first cases

- `AC406_DashboardUsesReferenceBentoAndActivityComposition`
- `AC411_AnalyticsScreenPreservesReferenceRegions`
- `AC412_PortalDashboardAndProfileMatchReferences`
- `AC416_DashboardAsyncStatesRemainDistinct`
- `AC417_UnbackedDashboardRegionsAreNotInteractive`
- `AC418_DashboardAndPortalControlsAreKeyboardAccessible`

## Done when

Staff and portal dashboard routes match their references at desktop, tablet and mobile widths and
existing report/portal tests remain green.

## Exact files

- Staff dashboard: `frontend/projects/admin-app/src/app/features/dashboard/dashboard.component.{ts,html,spec.ts}`.
- Reports: `features/reports/{ticket-volume,sla-performance,agent-performance}-report.component.*`.
- Portal home/dashboard: `frontend/projects/portal-app/src/app/features/{home,dashboard}/*.component.*`.
- Portal profile target: `frontend/projects/portal-app/src/app/features/account/` if the route
  already exists; otherwise record the missing route before adding it.
- Reference files: `stitch_smart_support_ticketing_crm/{agent_dashboard_overview,admin_dashboard,management_analytics_sla_performance,user_dashboard,user_profile_settings}/code.html`.

## Live implementation example

For `dashboard.component.html`, keep the existing dashboard API signal and project it into the
mockup's bento regions. A missing CSAT/chart value uses the shared unavailable placeholder in the
same card position. It must not use a fake `72%` value merely to make the screenshot look complete.

## Execution commands

```text
cd frontend
npx ng test admin-app --watch=false --include='**/features/dashboard/**/*.spec.ts'
npx ng test admin-app --watch=false --include='**/features/reports/**/*.spec.ts'
npx ng test portal-app --watch=false --include='**/features/home/**/*.spec.ts'
npx ng test portal-app --watch=false --include='**/features/dashboard/**/*.spec.ts'
```
