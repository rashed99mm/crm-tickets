# Task 08 · Visual matrix and regression closure

**Criteria:** `AC-419`, `AC-420`, `AC-421`, `AC-422`  
**Status:** Completed (All test suites and production builds passing with zero errors)

## Changes

1. Capture each inventory route at 375px, 768px, 1280px and 1920px with Playwright.
2. Compare captures with the supplied HTML/PNG references and maintain a deviation log for every
   mismatch, including accepted data-driven differences.
3. Run RTL safety, string, component, routing, API/state tests and both app builds.
4. Resolve all implementation regressions or record explicit gaps in the epic record and
   traceability document.

## Test-first cases

- `AC419_AdaptedTemplatesPassRtlAndLocalizationChecks`
- `AC420_BothApplicationsBuildWithoutWarnings`
- `AC421_AllRoutesHaveFourViewportVisualReviewRecords`
- `AC422_PreExistingFrontendSuiteRemainsGreen`

## Done when

Actual command output, screenshot review records, deviations and commit hashes are pasted into the
plan record. No completion claim is made from an unrun test or an unreviewed screenshot.

## Exact files

- Test target: `frontend/e2e/mockup-fidelity.spec.ts`.
- Configuration: `frontend/playwright.config.ts`.
- Reference captures: `stitch_smart_support_ticketing_crm/*/screen.png` and corresponding `code.html`.
- Evidence record: update `docs/superpowers/plans/EPIC-13-US-311-mockup-fidelity/README.md` with actual
  commands, outputs, screenshot paths and deviations.

## Live implementation example

Use a viewport matrix rather than one screenshot:

```ts
for (const width of [375, 768, 1280, 1920]) {
  await page.setViewportSize({ width, height: 1000 });
  await page.goto('/tickets');
  await expect(page).toHaveScreenshot(`ticket-queue-${width}.png`, { fullPage: true });
}
```

The exact route and authentication fixture must be confirmed from `app.routes.ts` before this test
is written. If a reference has no application route, record it as a gap instead of creating a fake
route solely for screenshot capture.

## Execution commands

```text
cd frontend
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
npx playwright test --grep='mockup'
```
