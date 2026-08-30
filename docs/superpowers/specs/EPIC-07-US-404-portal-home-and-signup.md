# Portal home + signup flow (FEAT-22, customer portal — frontend slice S3)

**Date:** 2026-08-27
**Feature:** `FEAT-22` Customer portal (slice S3 — customer authentication)
**Stories:** `US-410` (portal login screen, frontend), `US-401`/`US-402` (registration + login, backend already present via `POST /api/Auth/register` / `POST /api/Auth/login`), plus a new, previously-inspecified **public home** page and a **signup** screen.
**Status note:** This spec is written *before* implementation, per the SDD gate. The acceptance criteria below are scoped to this document (`ASG-n`), each naming the requirement it satisfies, so they are traceable to tasks and tests without colliding with the already-allocated global `AC-n` numbers.

## Problem

A new visitor to the portal today lands on the unconditional `/login` screen. There is **no public
home page** — nothing that explains what the portal is or lets a visitor choose to sign in or create
an account — and **no signup screen**: a customer who does not yet have credentials has nowhere to
register. The backend already exposes `POST /api/Auth/register` and the portal shell (dashboard,
submit, tickets, KB) exists behind `authGuard`, so the missing pieces are exactly the discovery
surface (home) and the entry point (signup).

The existing portal login (`features/auth/login.component`) posts to `AuthApi.signIn` and redirects
to the protected area. A registration path should carry the same field-keyed-validation and error
conventions, and — since a freshly registered user has nowhere to go until they hold a session — it
should sign the user in and land them in the protected area.

The user has also asked that the signup form collect a **phone number**. The current
`POST /api/Auth/register` contract (`RegisterRequest`: `email, username, password, firstName,
lastName`) does not accept one, so the contract and the `ApplicationUser` it creates must be extended
to carry it (see Assumptions).

## Assumptions

A1. **The home page is a public landing page, not the authenticated dashboard.** Today `/` is the
   protected dashboard inside `PortalShell` (guarded by `authGuard`). This spec moves public content
   in front of authentication: `/` becomes a public landing page offering "Sign in" and "Create an
   account", and the authenticated area moves under `/app` (redirecting `/app` → the existing
   dashboard). A logged-in visitor who hits `/` is redirected to `/app` rather than shown the landing
   page again. Recorded because it changes existing routing, not because the change is risky.

A2. **Signup = register, then sign in.** `POST /api/Auth/register` returns `201 Response<Guid>`
   (the new user id) with **no tokens** (`RegisterCommandHandler` returns `user.Id`). The frontend
   therefore calls `register(...)` and then `signIn(...)` with the same credentials before routing to
   `/app`. This is the observable "sign me up and take me in" contract the user asked for. It means
   the successful-signup path performs two HTTP calls; if the auto-login fails after a successful
   registration the user is *still registered* and is routed to `/login` rather than being told the
   whole flow failed.

A3. **Phone is optional, backend-side, and stored on the user's profile.** The `RegisterRequest`/
   `RegisterCommand` gain an optional `string? PhoneNumber`. The handler sets it on the created
   `ApplicationUser.PhoneNumber` (a base `IdentityUser` property, currently never set by `Create`).
   The form may leave it blank; blank is sent as `null`, never `""`. The phone is **not** used for
   anything else this pass — no OTP, no customer-record link (that is `US-401`'s own customer-record
   work, out of scope here).

A4. **Validation mirrors the server.** The signup form's client validators and the server's
   `RegisterCommandValidator` must agree, so a field accepted by the UI is accepted by the server:
   email (required, email, ≤255), username (required, 3–50, `^[a-zA-Z0-9_]+$`), password (required,
   8–100, one uppercase, one lowercase, one digit), first name and last name (required, ≤100), phone
   (optional). When they disagree, the server is the truth — server field errors map onto the controls
   via `ApiError.fieldError(name)`.

A5. **`/signup` sits next to `/login` as a public route** outside `authGuard`, and both link to each
   other. The protected `PortalShell` and all its children keep producing exactly the same paths they
   do today, only relocated under `/app`.

## Out of scope

- The customer-record side of registration (`US-401`'s "create a `Customer` row"): `POST
  /api/Auth/register` today creates an identity user only. Linking portal registration to a real
  `Customers` row is `US-401`'s backend scope and is not this feature.
- OTP / email verification / password reset; phone-based anything.
- A customer-facing "profile" screen or any UI to edit the captured phone after signup.
- Editing the existing login screen beyond pointing it at the new routes and adding the link to
  signup.
- Anything that changes `/app`'s dashboard, submit, tickets, or KB behaviour.

## Acceptance criteria

ASG-1. Given a visitor opens the portal root while logged out, then they see a public **home**
(`US-410` entry point) page describing the portal, with a "Sign in" action and a "Create an account"
action; `/` does not redirect them to `/login`.

ASG-2. Given a visitor is already signed in and navigates to `/`, then they are redirected to the
protected area (`/app`) rather than shown the landing page.

ASG-3. Given a logged-out visitor chooses "Create an account", then they land on `/signup` with a
form for first name, last name, email, username, phone and password, and a link back to sign in.

ASG-4. Given the signup form with any empty required field, a malformed email, an over-length value,
an invalid username, or an under-8/48-weak password, when submitted, then the matching client
validation error appears on that control and **no HTTP request is sent**.

ASG-5. Given a valid signup submission, when the request completes, then `POST /api/Auth/register`
was called with `{ email, username, password, firstName, lastName, phoneNumber }` (phone sent as
`null` when blank) and, on success, the user is signed in and routed to `/app`.

ASG-6. Given the server rejects a submission (e.g. duplicate email → 409, duplicate username →
409, or a field-level validation error → 400), then each server field error appears on the control it
names and a non-field rejection (conflict) is shown as a form-level message; the user is **not**
routed anywhere and remains on `/signup`.

ASG-7. Given a successful registration whose automatic sign-in then fails, then the failure is shown
on `/login` (with a "your account was created — sign in" cue) rather than being reported as a failed
registration. (A3's two-step behaviour.)

ASG-8. **Backend (phone in the contract):** when `POST /api/Auth/register` is called with an optional
`phoneNumber`, then the resulting `ApplicationUser` has `PhoneNumber` set to the trimmed value (or
stays `null` when absent); validation accepts blank/absent phone and rejects an over-length phone, and
a phone that passes the UI is accepted by the server.

## Design

### Backend (small, additive)

**Edit** `RegisterRequest` (Dtos) and `RegisterCommand` to append `string? PhoneNumber`.
**Edit** `AuthController.Register` to pass `request.PhoneNumber`.
**Edit** `RegisterCommandValidator` to add: optional phone, `MaximumLength(20)` when present.
**Edit** `RegisterCommandHandler`: after `ApplicationUser.Create(...)`, set
`user.PhoneNumber = NormalizePhone(request.PhoneNumber)` (trim; `null`/blank → `null`) before
`CreateAsync`. No new error codes; existing `EMAIL_EXISTS` / `USERNAME_EXISTS` conflicts unchanged.
No migration (`PhoneNumber` already exists on `AspNetUsers` via `IdentityUser`).

### Frontend — new/edited files (portal-app + common)

- **`common/auth/auth.api.ts`**: add `register(payload): Observable<{ id: string }>` posting to
  `POST /api/Auth/register` with `{ email, username, password, firstName, lastName, phoneNumber }`
  (phone `null` when blank). Add `RegisterRequest` interface. (+ `.spec.ts`)
- **`common/i18n/translations.ts`**: add `portal.home.*` and `portal.signup.*` keys, en **and** ar.
- **`features/home/home.component.{ts,html,spec.ts}`**: public landing page — brand, one-line
  description, primary "Sign in" and secondary "Create an account" actions. On mount, if
  `SessionStore.isAuthenticated()` then redirect to `/app`.
- **`features/auth/signup.component.{ts,html,spec.ts}`**: the form (A4 validators), `AuthApi.register`
  then `AuthApi.signIn`, `SessionStore.signIn`, route to `/app`; server errors via
  `ApiError.fieldError`; conflict at form level.
- **`app.routes.ts`**: outside the guard — `{ path: '', component: Home }`, `{ path: 'signup',
  component: Signup }`, `{ path: 'login', component: Login }`; the guarded children move under
  `{ path: 'app', component: PortalShell, canActivate: [authGuard], children: [...] }`; `/app` empty
  → dashboard; legacy `/` redirect to `/app` when authenticated (handled inside Home, ASG-2).
- **`features/auth/login.component.{ts,html}`**: add a "Create an account" link to `/signup`; keep
  its registered-user-after-failed-auto-login handling (ASG-7).

### Error behavior

No new backend error codes (ASG-8). Frontend surfaces the existing register/login error surface:
field-keyed `VALIDATION_ERROR` (400), `EMAIL_EXISTS`/`USERNAME_EXISTS` as form-level conflict
messages (ASG-6), generic failure as a non-field message. The three async states (loading / empty /
error) apply to the signup submit and login flows as they already do for login.

## Testing

| Level | Covers |
|---|---|
| Backend integration (WebApplicationFactory) | `ASG-8` — register carrying `phoneNumber` persists it; absent/blank phone stays `null`; over-length phone → 400; phone no longer silently dropped from the contract |
| Component (Vitest + `HttpTestingController`) | `ASG-1`, `ASG-3`, `ASG-4`, `ASG-5`, `ASG-6`, `ASG-7` — home renders + authenticated-redirect; signup form renders fields incl. phone; client validation blocks invalid submit (`expectNone`); valid submit posts register then login; server field error under its control; conflict at form level; register-success-but-login-failure lands on `/login` with the account-created cue |
| Routing/guard | `ASG-2` — authenticated visit to `/` goes to `/app`; logged-out visit to `/signup` and `/login` allowed; protected children unreachable without auth |

Tests are named after the criterion (e.g. `ASG5_ValidSubmit_PostsRegisterThenLogin`,
`ASG8_Register_PersistsPhoneNumber`).
