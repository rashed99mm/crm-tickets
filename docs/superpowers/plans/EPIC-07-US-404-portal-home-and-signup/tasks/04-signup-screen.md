# Task 04 — Signup screen (form + register → login → `/app`)

**Story:** `US-401`, `US-410` · **Criteria:** `ASG-3`, `ASG-4`, `ASG-5`, `ASG-6`, `ASG-7`
**Status:** done; verified by the end-of-work test run

## Files

- Create `frontend/projects/portal-app/src/app/features/auth/signup.component.{ts,html,spec.ts}`.
- Modify `frontend/projects/portal-app/src/app/features/auth/login.component.{ts,html}` — link to
  `/signup` and handle the "account created, sign in" redirect cue (ASG-7).

## Implementation sequence

1. Reactive form: first name, last name, email, username, phone, password. Client validators mirror
   the server (`RegisterCommandValidator`, spec A4): email required+email+≤255; username required,
   3–50, `^[a-zA-Z0-9_]+$`; password required, 8–100, one uppercase/lowercase/digit; names required
   ≤100; phone optional. Submit disabled while invalid *and* while in flight; errors shown after
   touch; server field errors via `ApiError.fieldError(name)`.
2. Submit: `AuthApi.register({ ...phoneNumber: phone || null })`; on success call
   `AuthApi.signIn(email, password)`, `SessionStore.signIn`, route to `/app` (ASG-5).
3. Register failure: field errors on their controls; `EMAIL_EXISTS`/`USERNAME_EXISTS` conflict at
   form level; stay on `/signup` (ASG-6).
4. Register success but login failure: route to `/login` with a query flag the login screen reads to
   show "your account was created — sign in" (ASG-7).
5. Keys from task 06; no physical-direction utilities.

## Tests and evidence

- `ASG3_RendersAllFields_IncludingPhone_AndSignInLink`
- `ASG4_InvalidForm_SendsNoRequest` (`expectNone`) for empty/weak/oversized/bad-username cases
- `ASG5_ValidSubmit_PostsRegisterThenLogin_AndNavigatesToApp`
- `ASG6_ServerFieldError_UnderItsControl`, `ASG6_Conflict_AtFormLevel_StaysOnSignup`
- `ASG7_RegisterOk_LoginFail_RoutesToLogin_WithCue`

Run with the portal suite at the end.
