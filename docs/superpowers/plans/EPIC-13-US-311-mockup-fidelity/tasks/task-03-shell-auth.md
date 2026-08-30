# Task 03 · Adapt shell, landing and signup screens

**Criteria:** `AC-405`, `AC-412`, `AC-418`  
**Status:** Completed (All home/login/signup specs passed)

## Changes

Adapt `command_center`, `command_center_crm_landing_page` and `create_your_account` into the
existing shell/auth routes. Preserve typed reactive form validation, server field errors, auth
signals, locale switching and existing navigation guards.

## Test-first cases

- `AC412_LandingAndSignupMatchReferenceComposition`
- `AC412_SignupPreservesValidationAndSubmissionBehaviour`
- `AC418_SignupAndLandingRemainKeyboardReachable`

## Done when

Desktop, tablet and mobile screenshots are reviewed, auth tests pass, and no designed unavailable
region is exposed as a misleading enabled control.

## Exact files

- Staff shell: `frontend/projects/admin-app/src/app/layout/shell.component.html`.
- Staff auth: `frontend/projects/admin-app/src/app/features/auth/login.component.ts`,
  `login.component.html`, `login.component.spec.ts`.
- Portal landing/auth: `frontend/projects/portal-app/src/app/features/home/home.component.*`,
  `features/auth/signup.component.*`, `features/auth/login.component.*`.
- Route checks: `frontend/projects/portal-app/src/app/app.routes.ts` and
  `frontend/projects/portal-app/src/app/app.spec.ts`.

## Live implementation example

Port the `create_your_account` form order into `signup.component.html`, but keep the existing typed
form in `signup.component.ts`. A visual change may rename a label or add a wrapper; it must not move
validation into the template or bypass `AuthApi`. Use `CsInputField` for the styled fields and map
server errors through `ApiError.fieldError(...)` as the current auth flow does.

## Execution commands

```text
cd frontend
npx ng test admin-app --watch=false --include='**/auth/login.component.spec.ts'
npx ng test portal-app --watch=false --include='**/auth/signup.component.spec.ts'
npx ng build admin-app
npx ng build portal-app
```
