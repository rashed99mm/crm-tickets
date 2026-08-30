# Task 07 · Responsive, accessibility and i18n hardening

**Criteria:** `AC-404`, `AC-413`, `AC-414`, `AC-415`, `AC-418`, `AC-419`, `AC-420`  
**Status:** Completed (All RTL, responsiveness and accessibility tests passed)

## Changes

Run a route-by-route responsive audit at 375px, 768px, 1280px and 1920px. Fix overflow, clipped
content, focus order, drawer behaviour, table transformations, Arabic mirroring, translated labels
and loading/empty/error branches. Add no-hardcoded-string and RTL regression coverage for every
new template.

## Test-first cases

- `AC413_AllRoutesFitMobileViewportAndStackCorrectly`
- `AC414_AllRoutesFitTabletViewport`
- `AC415_AllRoutesPreserveDesktopComposition`
- `AC418_AllAdaptedRoutesMeetKeyboardAndLandmarkContract`

## Done when

No target viewport has horizontal overflow, all visible text is localized, Arabic layout mirrors,
and every state/control has an accessible name and focus path.

## Exact files

- Templates under `frontend/projects/admin-app/src/app/**/*.html` and
  `frontend/projects/portal-app/src/app/**/*.html`.
- Shared checks: `frontend/projects/common/src/lib/testing/rtl-safety.spec.ts`,
  `no-hardcoded-strings.spec.ts`, `bilingual-ui.spec.ts`.
- Existing responsive precedent: `frontend/projects/admin-app/src/app/layout/shell.component.*`.
- Visual route configuration: `frontend/playwright.config.ts` and `frontend/e2e/`.

## Live implementation example

For every desktop `grid-cols-3` region, add an explicit mobile contract such as
`grid-cols-1 lg:grid-cols-3`; for a table, use `overflow-x-auto` only when the reference requires a
wide table, otherwise render the planned mobile card rows. Verify with `page.evaluate(() =>
document.documentElement.scrollWidth <= document.documentElement.clientWidth)` at 375px.

## Execution commands

```text
cd frontend
npx ng test common --watch=false --include='**/testing/*.spec.ts'
npx playwright test --project=chromium --grep='responsive'
```
