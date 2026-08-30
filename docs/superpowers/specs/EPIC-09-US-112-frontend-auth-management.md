# Frontend auth management — login, logout, refresh, change password

**Date:** 2026-08-25
**Criterion ids:** `FAM-n`. Separate prefix; does not collide with `AC-n`, `FND-n`, `AUTH-n`,
`BASE-n` or `FE-n`.
**Builds on:**
[`EPIC-13-US-311-frontend-realignment.md`](./EPIC-13-US-311-frontend-realignment.md) (`FE-1`..`FE-16`)
— that spec is **adopted as-is** for the contract layer, sign-in, session and refresh. This document
does not repeat its reasoning; it cites it, closes the one gap it deliberately left open
(`FE-15` — change password parked), and records what a real forgot-password flow would need without
building it yet.

## Why this document exists

`FE-1`..`FE-16` fixed the wire contract mismatch between the admin frontend and the adopted CCE
platform backend: envelope shape, sign-in route, session fields, and a single-flight refresh — but
**deliberately parked** the change-password screen (`FE-15`) because no backend endpoint existed for
it. That gap is now closed: a `POST /api/Auth/change-password` endpoint is added to the backend, and
the already-written (but unrouted) `change-password.component` is wired to it.

Forgot-password (a logged-out user recovering access without knowing their current password) is a
different feature with a different failure mode — it needs a way to prove the requester owns the
email address, which in this codebase means sending mail, and nothing here sends mail. It is staged
as **Slice 2** of this same document: specified now so the gap is visible and estimable, not built
until the assumption below is either accepted or replaced.

## Assumptions

- **M1.** `FE-1`..`FE-16`'s re-pointed contract (envelope, `AuthApi`, `SessionStore`, refresh
  single-flight) is correct and does not change here. Any defect found while wiring change-password
  against it is fixed in place, not re-specified.
- **M2.** Only a signed-in user can change their own password (`AUTH-15`..`AUTH-17` from
  `EPIC-09-US-112-auth-management-design.md`, restated here in the current role vocabulary: `Admin`/
  `User`, per `FE-2` in the realignment spec). There is no "change someone else's password" screen —
  that is `UsersController`'s existing `PUT` update surface, out of scope here.
- **M3.** Changing a password does **not** revoke existing refresh tokens in this slice. Revoking
  every other session on password change is the correct security posture, but the backend's
  `RefreshTokenService` currently only exposes revocation by token value or by user id
  (`RevokeAllUserRefreshTokensAsync` exists and **will** be called — see `FAM-4`), so this is a
  deliberate use of an existing capability, not a gap.
- **M4 (Slice 2 only).** No email provider exists in this codebase (confirmed: no
  `IEmailSender`, no SMTP configuration, `PlatformSettings` holds none). A real forgot-password flow
  is genuinely blocked on that decision, which is why Slice 2 is specified but not scheduled.

## Slice 1 — Change password (in scope, built now)

### Acceptance criteria

Priority: **P0** must ship.

- **FAM-1** (P0) Given a valid bearer token, a correct current password and a new password meeting
  the configured strength rule, `POST /api/Auth/change-password` returns `200` with
  `Result<Unit>.Success()`, and the new password authenticates on the next `POST /api/Auth/login`.
- **FAM-2** (P0) Given a wrong current password, the endpoint returns `400` with
  `Result<Unit>.Failure` carrying a field-level detail keyed `currentPassword` — **never** `401`.
  The caller is authenticated; one field is wrong. This mirrors `AUTH-16`'s reasoning under the
  actual `Result<T>`/`details` shape (`FE-5`), not the retired envelope.
- **FAM-3** (P0) Given a new password that fails Identity's configured strength rule, `400` with a
  detail keyed `newPassword`.
- **FAM-4** (P0) On success, every refresh token belonging to the user is revoked
  (`IRefreshTokenService.RevokeAllUserRefreshTokensAsync`), so a stolen refresh token issued before
  the change stops working. The access token already in the caller's hand keeps working until it
  expires — access tokens are not revocable in this design, which is a pre-existing property of the
  short-lived JWT approach and not a new gap.
- **FAM-5** (P0) No response body, and no log line at any level, ever contains either password.
  Restates `AUTH-17` against the current logging call sites (`LoginCommandHandler`,
  `RefreshTokenCommandHandler` and the new handler here all log user ids and outcomes, never
  credential values).
- **FAM-6** (P0) Given no token, `401` — the route carries `[Authorize]`, not `[AllowAnonymous]`.
- **FAM-7** (P0) The frontend's `change-password.component` (already written) is routed at
  `/account/password`, reachable from the shell's account area, and:
  - submits through `StaffApi.changeOwnPassword`, re-pointed to `POST /api/Auth/change-password`
    with body `{ currentPassword, newPassword }`;
  - shows the field error from `FAM-2`/`FAM-3` on the named control, not a banner (`FE-8`/`FE-21`'s
    pattern, applied here);
  - clears both fields on success and on failure, so neither password lingers in the DOM (`FAM-5`
    extended to the client);
  - disables submit while the request is in flight and while the form is invalid.
- **FAM-8** (P1) Signing out from the shell after changing password, then signing in again with the
  **old** password, fails — proves `FAM-4` end to end, not just at the unit level.

### Design

**Backend.** New `ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword)` /
handler in `Application/Features/Auth/Commands/ChangePassword/`, following the existing
`LoginCommandHandler` shape exactly: constructor-injected `IIdentityUserService`,
`IRefreshTokenService`, `ILocalizationService`, `ILogger`. `IIdentityUserService` needs one addition —
`ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword)` — implemented
in `IdentityUserService` over `UserManager<ApplicationUser>.ChangePasswordAsync`, which already
validates the current password and the configured `PasswordOptions` and returns an
`IdentityResult` this codebase already knows how to map (`ToOperationResult`, used by every other
`IIdentityUserService` method). Two new `ApplicationErrors.Auth` keys:
`CURRENT_PASSWORD_INCORRECT`, `PASSWORD_TOO_WEAK`, both `ErrorType.Validation` so they land in
`Result.Error.Details` the way `ValidationBehavior` already produces field details for other
commands — the handler builds the `Error` with a `Details` dictionary keyed `currentPassword` /
`newPassword` directly, since `UserManager` reports the failure outside FluentValidation's pipeline.

Controller action: `AuthController.ChangePassword`, `[HttpPost("change-password")]`,
`[Authorize]`, reads the caller's id via `User.GetRequiredUserId()` (existing extension, used
nowhere yet in `AuthController` but already relied on elsewhere) — the user id is **never** taken
from the request body, matching `AUTH-19`'s "author from token" pattern applied to identity instead
of authorship.

**Frontend.** `StaffApi.changeOwnPassword` already exists and already posts
`{ currentPassword, newPassword }` — only its URL changes, from the retired
`/api/auth/change-password` guess to the real `/api/Auth/change-password` (`FE-9`'s casing
convention: the reference's routing is case-sensitive-by-convention `PascalCase` segments,
confirmed against `AuthController`'s `[Route("api/[controller]")]` resolving to `api/Auth`).
`change-password.component` needs no logic change — `FE-8`'s detail-to-control mapping already
handles `currentPassword`/`newPassword` once the interceptor speaks the real shape — only a route
entry in `admin-app/app.routes.ts` (currently absent per `FE-15`) and a nav link from the shell.

### Out of scope (Slice 1)

Changing another user's password (that is `UsersController`'s update surface) · password history /
reuse prevention · forcing re-authentication on other devices beyond refresh-token revocation
(`FAM-4`'s access-token caveat is accepted, not solved) · rate limiting the endpoint beyond the
`[Authorize]` boundary (an authenticated attacker already has a session; rate limiting is what
protects `login`, which already has it).

## Slice 2 — Forgot password (specified, not scheduled)

Recorded so the gap is visible and estimable rather than silently absent. **Do not implement without
a decision on `M4`.**

### What it would need

- **FAM-9** (deferred) `POST /api/Auth/forgot-password` accepting an email, always returning `200`
  regardless of whether the account exists — mirroring `AC-2`'s "failure discloses nothing" rule,
  because a distinguishable response here is a user-enumeration oracle exactly as it would be on
  sign-in.
- **FAM-10** (deferred) A `UserManager<ApplicationUser>.GeneratePasswordResetTokenAsync` token,
  emailed via an `IEmailSender` port this codebase does not yet have an implementation for —
  `PlatformSettings` could hold provider configuration, but nothing wires it today.
- **FAM-11** (deferred) `POST /api/Auth/reset-password` accepting the emailed token plus a new
  password, calling `UserManager.ResetPasswordAsync`, and revoking all refresh tokens on success
  (same `FAM-4` reasoning).
- **FAM-12** (deferred) A frontend `/forgot-password` and `/reset-password` route, neither gated by
  `authGuard` — the entire point is working without a session.

### Why it is blocked, not merely unscheduled

Every option costs something specific, recorded so choosing later is a decision and not a default:

| Option | Cost |
|---|---|
| Real SMTP/provider (SendGrid, SES, etc.) | New dependency, new secret, new failure mode (mail delivery) to handle without leaking account existence |
| Dev-only "return the token in the response" fallback | Works with zero infrastructure but is **not shippable** — it defeats the entire security property the flow exists for, so it can only ever be a local-dev toggle guarded out of any real deployment |
| Skip it, tell users to contact an administrator | Zero cost, but re-confirms `AUTH`'s `M4`/`B3`-adjacent assumption that there is no self-service account recovery at all — a real product decision, not a technical shortcut |

This spec takes no position among the three. Slice 2 stays unscheduled until the assessment or the
product owner picks one.

## Testing

Backend: xUnit unit test for `ChangePasswordCommandHandler` (wrong current password → validation
error keyed `currentPassword`; weak new password → keyed `newPassword`; success → refresh tokens
revoked, called through a fake `IRefreshTokenService`) plus one integration test through
`CustomerSupport.Tests` hitting the real endpoint with the seeded administrator, covering
`FAM-1`, `FAM-2`, `FAM-4`, `FAM-6`.

Frontend: `change-password.component.spec.ts` (already exists per `FE-15`'s parked state) gets its
assertions un-skipped against the real URL and, if needed, extended for `FAM-7`'s field-error and
clear-on-failure behaviour with `HttpTestingController`. One routing test proves `/account/password`
resolves inside the authenticated shell.

`FAM-8` is a manual smoke check against the running host, reported as exactly that — not an
automated test — matching the realignment spec's own rule for `FE-1`–`FE-3`.
