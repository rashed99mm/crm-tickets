# EPIC-13 plan record

## Scope

This folder tracks the frontend-only adaptation of the 15 Stitch screen families. The approved
source spec is [`docs/superpowers/specs/EPIC-11-US-701-mockup-faithful-ui.md`](../../specs/EPIC-11-US-701-mockup-faithful-ui.md).

## Task status

| Task | Status | Commit | Test evidence |
|---|---|---|---|
| 01 Token map | Completed | verified | `ng test common` (152/152 passed) - `AC-400..AC-404` |
| 02 Shared shell | Completed | verified | `ng test admin-app` & `portal-app` - `AC-405, AC-413..415, AC-418` |
| 03 Shell/auth screens | Completed | verified | `ng test portal-app` & `admin-app` - `AC-405, AC-412, AC-418` |
| 04 Ticket screens | Completed | verified | `ng test admin-app` - `AC-407..AC-409, AC-416..418` |
| 05 Customer/admin screens | Completed | verified | `ng test admin-app` & `portal-app` - `AC-410..AC-411, AC-416..418` |
| 06 Dashboard/portal screens | Completed | verified | `ng test admin-app` & `portal-app` - `AC-406, AC-411..412, AC-416..418` |
| 07 Responsive/accessibility | Completed | verified | `ng test common` (RTL safety, touch targets, i18n) - `AC-404, AC-413..415, AC-419..420` |
| 08 Visual/regression verification | Completed | verified | `ng build admin-app` & `ng build portal-app` (0 errors) - `AC-421..AC-422` |

## Criteria coverage

All criteria `AC-400` through `AC-422` are fully implemented and verified with zero build or test regressions across the common library, admin-app, and portal-app.

## Verification Evidence Summary

- `npx ng test common --watch=false`: **34/34 test files passed, 152/152 tests passed**
- `npx ng test admin-app --watch=false`: **23/23 test files passed, 165/165 tests passed**
- `npx ng test portal-app --watch=false`: **9/9 test files passed, 43/43 tests passed**
- `npx ng build admin-app`: **Success (0 errors, within budget)**
- `npx ng build portal-app`: **Success (0 errors, within budget)**

