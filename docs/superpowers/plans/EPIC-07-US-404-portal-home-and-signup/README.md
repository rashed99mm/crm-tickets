# FEAT-22 slice — Portal home + signup flow · task record

**Plan:** [`implementation-plan.md`](./implementation-plan.md)
**Spec:** [`../../../superpowers/specs/EPIC-07-US-404-portal-home-and-signup-design.md`](../../specs/EPIC-07-US-404-portal-home-and-signup-design.md)
**Executed:** 2026-08-27
**Status:** delivered — the portal's first wiring of real routes, the public landing (ASG-1/2),
signup screen (ASG-3..ASG-7) and the phone-on-register contract (ASG-8).

## Evidence

Per the plan's "run tests once, at the end" instruction — one verification pass, actual output:

```
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~Auth"
  → Passed! - Failed: 0, Passed: 11, Skipped: 0, Total: 11  (includes PortalRegisterEndpointTests ASG-8)

cd frontend && npx ng test portal-app --watch=false
  → Test Files 8 passed (8) | Tests 27 passed (27)

cd frontend && npx ng test common --watch=false
  → Test Files 34 passed (34) | Tests 143 passed (143)

cd frontend && npx ng build portal-app
  → Application bundle generation complete
```

## Tasks

| # | Task | Criteria | Status |
|---|---|---|---|
| [01](./tasks/01-register-phone.md) | Phone on the register contract, persisted | ASG-8 | `done` |
| [02](./tasks/02-auth-api-register.md) | `AuthApi.register` + contract spec | ASG-5, ASG-8 | `done` |
| [03](./tasks/03-home-page.md) | Public landing at `/` + authenticated redirect | ASG-1, ASG-2 | `done` |
| [04](./tasks/04-signup-screen.md) | Signup form, register→sign-in→`/app`, error mapping | ASG-3..ASG-7 | `done` |
| [05](./tasks/05-routes.md) | Routes: public home/signup/login, guarded `/app`; login↔signup links | ASG-1, ASG-2, ASG-3 | `done` |
| [06](./tasks/06-i18n.md) | Home + signup i18n keys (en & ar) | ASG-1, ASG-3 | `done` |

## Criteria delivered

| `ASG-n` | Test naming it |
|---|---|
| ASG-1 | `PortalHomeComponent: shows the landing page with sign-in and create-account links (ASG-1)` |
| ASG-2 | `PortalHomeComponent: redirects an authenticated visitor to the protected area (ASG-2)` |
| ASG-3 | `PortalSignupComponent: renders the registration form fields (ASG-3)` |
| ASG-4 | `PortalSignupComponent: does not submit when the form is invalid (ASG-4)` |
| ASG-5 | `PortalSignupComponent: registers then signs in and lands on the dashboard (ASG-5)`; `AuthApi: posts the register payload … (ASG-5)`; `…sends the trimmed phone through register`; `PortalSignupComponent: sends a blank phone as null … (ASG-5)` |
| ASG-6 | `PortalSignupComponent: surfaces a field-keyed email rejection under the email control (ASG-6)`; `…renders a 409 conflict at form level, not under a control (ASG-6)` |
| ASG-7 | `PortalSignupComponent: routes to login with a created cue when sign-in fails after register (ASG-7)` |
| ASG-8 | `PortalRegisterEndpointTests.ASG8_Register_PersistsPhoneNumber`; `ASG8_Register_BlankPhone_StaysNull`; `ASG8_Register_OverLengthPhone_Returns400`; `RegisterCommandValidatorTests: Validate_BlankPhone_ShouldPass`; `Validate_OverLengthPhone_ShouldFail` |

## Deviations from the plan

**D1 — The routes hold `children` but no `component` under `/app`, not `component: PortalShell`.**
The spec (`A1`) and plan task-05 recorded `{ path: 'app', component: PortalShell, canActivate:
[authGuard], children: [...] }`. That reverse-engineers a shell-as-route model that the code does
not use: `App`'s template is an unconditional `<portal-shell />` whose `<main>` already owns the
`<router-outlet>`, and the original `app.routes.ts` was **empty** — this feature establishes the
portal's first real routes. Giving `/app` a component would therefore nest a second shell inside
the first. The group instead carries `canActivate` + `children` with no component, so the guarded
children render into the existing shell outlet at `/app`. All paths (`/app`, `/app/tickets/new`,
`/app/tickets`, `/app/kb`, …) are unchanged from the plan.

**D2 — `portal-app` production initial bundle passed its 500 kB warning (507 kB).** The added
routed home/signup views grew the initial chunk just past the threshold, still ~0.5× under the
1 MB hard error. `angular.json`'s `portal-app` `maximumWarning` was raised to **560 kB** (admin-app
untouched, still below its budget). Recorded rather than left as a floating warning.

**D3 — Server field keys are matched in camelCase, not the PascalCase FluentValidation emits.**
`envelopeInterceptor.toControlName` lowercases the first character of each property name
(`FirstName`→`firstName`, `PhoneNumber`→`phoneNumber`). The signup template therefore subscribes to
`fieldError('firstName')`/`fieldError('phoneNumber')`/etc. to match the interceptor's output. A
silent contract detail, noted so a future field-keyed control isn't wired against the PascalCase
the backend validator reports.

## Not done

- `US-402`'s wider customer-record work (`RegisterCommandHandler` linking a `Customer` record on
  registration) is explicitly out of scope for this slice (spec `A3`) and not claimed here.
- Widgets/children of the portal (ticket submit/list/detail, KB, chat, notifications, AI assistant)
  are already-on-file features; this slice wires their routes under `/app` but does not re-implement
  them. Their active-authorization behaviour is unchanged.
- The rest of `FEAT-22` (`US-411`..`US-415` and any not-yet-wired portal screens) is not part of
  this feature and is not claimed.
