# Portal home + signup flow — implementation plan

**Date:** 2026-08-27
**Spec:** [`../../../superpowers/specs/EPIC-07-US-404-portal-home-and-signup-design.md`](../../specs/EPIC-07-US-404-portal-home-and-signup-design.md)
**Feature:** `FEAT-22` Customer portal (slice S3)
**Criteria:** `ASG-1` … `ASG-8`
**Runs as:** one vertical feature — a small backend contract change (phone on register) plus the
frontend screens that consume it. Per the SDD loop, both halves and their tests ship together.

## Why this exists

The portal's only entry is `/login`. There is no public home page and no signup screen, so a customer
without credentials has nowhere to register, and the backend's `POST /api/Auth/register` endpoint has
no consumer. The user asked for a home page and a signup flow, and that the signup form collect a
phone number — which today's register contract does not accept, hence the (small) backend half.

## Task map

| Task | Satisfies | Layer |
|---|---|---|
| 01 — add `PhoneNumber` to the register contract and persist it | `ASG-8` | Backend |
| 02 — `AuthApi.register` + contract spec test | `ASG-5`, `ASG-8` (client contract) | common |
| 03 — home landing page (public `/`) with authenticated-redirect | `ASG-1`, `ASG-2` | portal-app |
| 04 — signup screen (form + register→login→`/app`) | `ASG-3`, `ASG-4`, `ASG-5`, `ASG-6`, `ASG-7` | portal-app |
| 05 — routes: public home/signup/login outside guard, shell under `/app`; login link to signup | `ASG-1`, `ASG-2`, `ASG-3` | portal-app |
| 06 — i18n keys for home + signup (en & ar) | `ASG-1`, `ASG-3` | common |

Every `ASG-n` is covered by at least one task and at least one test named after it.

## Dependency order

05 depends on 03 (home component exists) and 04 (signup component exists) and 06 (keys). 04 depends
on 02 (`AuthApi.register`). 03 depends on 06. 01 is independent of the frontend (its test proves the
contract in isolation). Order: **01 → 02 → 06 → 03 → 05 → 04**, then the component/manual checks and
the single end-of-work verification run.

## Verification approach (per the user's instruction: run tests once, at the end)

No incremental test runs. When all tasks are implemented:

```text
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~Auth"
cd frontend && npx ng test portal-app --watch=false
cd frontend && npx ng test common    --watch=false
cd frontend && npx ng build portal-app
```

Evidence from these four commands is pasted into the README and the story files. Known
non-`portal-home-and-signup` full-suite interference that may surface in much larger runs
(shared-DB parallel classes) is reported honestly if it appears, not papered over.

## Files touched (intent)

**Backend.** `AuthRequests.cs` (`RegisterRequest`), `RegisterCommand.cs`, `AuthController.cs`,
`RegisterCommandValidator.cs`, `RegisterCommandHandler.cs`; tests in
`backend/tests/CustomerSupport.Tests/Integration/AuthEndpointTests.cs` (or nearest auth suite —
verify on sight).

**common.** `auth/auth.api.ts` (+ `.spec.ts`), `i18n/translations.ts`, `public-api.ts` (only if a
new type needs exporting).

**portal-app.** `features/home/home.component.{ts,html,spec.ts}`,
`features/auth/signup.component.{ts,html,spec.ts}`, `app.routes.ts` (+ its spec),
`features/auth/login.component.{ts,html}`.

## Load-bearing caveat

The `AuthApi.signIn` path in `portal-app` already posts to `/api/Auth/login`; the new `/signup` and
home routes add to that surface without changing it. The success-path register→login→redirect is the
observable "sign up and take me in" behaviour the user chose, and ASG-7 pins the failure mode when the
second call fails.
