# Task 05 — Routes: public home/signup/login outside the guard; shell under `/app`

**Story:** `US-410` · **Criteria:** `ASG-1`, `ASG-2`, `ASG-3`
**Status:** done; verified by the end-of-work test run

## Files

- Modify `frontend/projects/portal-app/src/app/app.routes.ts` (+ its spec, if present).

## Implementation sequence

1. Outside the guard: `{ path: '', component: Home }`, `{ path: 'signup', component: Signup }`,
   `{ path: 'login', component: Login }`.
2. Guarded children move under `{ path: 'app', component: PortalShell, canActivate: [authGuard],
   children: [...] }`; empty `/app` → the existing dashboard. All existing leaf paths (submit,
   tickets, `tickets/:id`, kb, `kb/:id`) unchanged, just relocated under `/app`.
3. Update the shell's nav/links and the login/home links so every internal `routerLink` that pointed
   at `/tickets`, `/kb` etc. now points under `/app` (the shell already uses relative paths — verify).

## Tests and evidence

- `ASG1_Root_RendersHome_LoggedOut`
- `ASG2_AuthenticatedRoot_RedirectsToApp`
- `ASG3_Signup_Route_Reachable_LoggedOut`; protected children unreachable without auth.

Run with the portal suite at the end.
