# Task 7 — Evidence gate

**Satisfies:** FN-1 … FN-8

## Steps

1. Build both apps with warnings-as-errors:
   `npx ng build admin-app` and `npx ng build portal-app`.
2. Run the unit suites:
   `npx ng test common --watch=false`, `npx ng test admin-app --watch=false`,
   `npx ng test portal-app --watch=false`.
3. Manual smoke (record output): start `CustomerSupport.InternalApi` (5074) and
   `CustomerSupport.ExternalApi` (5095), log in to each app, trigger an in-app dispatch (e.g. via the
   backend `POST /api/Notifications` as Admin, or a feature that dispatches InApp), and confirm the
   bell updates live and after a reload.
4. Update `docs/superpowers/plans/INDEX.md` and the FEAT-15 story status from **observed** output only.

## Run
See steps above.

## Expected
Both apps build clean; unit suites pass; live + hydrated in-app notifications appear in both bells.
