# Task 03 — Public home / landing page

**Story:** `US-410` (entry point) · **Criteria:** `ASG-1`, `ASG-2`
**Status:** done; verified by the end-of-work test run

## Files

- Create `frontend/projects/portal-app/src/app/features/home/home.component.{ts,html,spec.ts}`.

## Implementation sequence

1. Component: brand, one-line description, primary "Sign in" (`/login`) and secondary "Create an
   account" (`/signup`) actions, plus a language switcher (bilingual access to the landing).
2. On mount, if `SessionStore.isAuthenticated()` then `router.navigateByUrl('/app')` (ASG-2).
3. Uses `TranslatePipe` keys from task 06; no physical-direction utility classes.

## Tests and evidence

- `ASG1_LoggedOut_ShowsLanding_WithSignInAndSignUp` — renders both actions, does not redirect to login.
- `ASG2_Authenticated_RedirectsToApp` — seeded session, on-mount navigation to `/app`.

Run with the portal suite at the end.
