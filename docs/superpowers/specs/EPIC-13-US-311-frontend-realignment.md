# Frontend realignment — re-pointing the Angular workspace at the adopted platform

**Date:** 2026-08-25
**Criterion ids:** `FE-n`. Separate prefix; does not collide with `AC-n`, `FND-n`, `AUTH-n` or `BASE-n`.
**Relates to:** `EPIC-12-US-000-crm-platform-baseline-design.md`, which adopted the platform and explicitly
placed the Angular frontend **out of scope**. This document is that deferred half.

## Why this document exists

`EPIC-12-US-000-crm-platform-baseline-design.md` recorded one line under Out of scope:

> The Angular frontend, which still targets the previous API contract and will need re-pointing.

That sentence hides a total disconnect. The frontend was written against the hand-built
`Response<T>` envelope and the `Agent`/`Supervisor` role model, both of which the adopted platform
discarded. **Nothing in the admin app can currently reach the backend.** This spec is written before
the code that fixes it, so the normal rule resumes here exactly as the baseline document promised.

## Problem

Every axis of the wire contract differs, and the differences were verified by reading both sides
rather than assumed:

| Axis | Frontend targets | Backend serves | Source |
|---|---|---|---|
| Envelope | `{success, code, message{ar,en}, data, errors[], traceId}` | `{isSuccess, data, error{code, messageAr, messageEn, type, details}}` | `api-response.ts` vs `Contracts/Result.cs` |
| Field errors | `errors[]` of `{field, code, message}` | `error.details` as `dict<string, string[]>` | `ExceptionHandlingMiddleware.cs:78` |
| Sign-in | `POST /api/auth/sign-in` | `POST /api/Auth/login` | `auth.api.ts:30` vs `AuthController.cs:41` |
| Activation | `POST /api/users/{id}/activate` | `PUT /api/Users/{id}/activate` | `staff.api.ts:42` vs `UsersController.cs:172` |
| Roles | `Agent`, `Supervisor` | `Admin`, `User`, `ContentManager`, `StateRepresentative` | `AuthorizationExtensions.cs:8` |
| Staff identity | `displayName` | `firstName` + `lastName` + required `username` | `staff.api.ts:8` vs `UserDtos.cs:17` |
| Session | access token only | `accessToken` + `refreshToken` + both expiries | `AuthDtos.cs:3` |
| Change password | `POST /api/auth/change-password` | **no such endpoint** | verified absent across `src/` |

Two further facts constrain the work:

1. **The host answers nothing.** Every request returns 500 `INTERNAL_ERROR`, including
   `/openapi/v1.json`. The baseline spec recorded this as an open defect.
2. **Two screens in the working tree were built against the dead contract.** `features/users/` and
   `features/account/` are untracked and call routes that do not exist.

## Assumptions

- **F1.** The backend is treated as **fixed and authoritative**. Where the two disagree, the frontend
  moves. This follows from the baseline decision to keep the reference's `Result<T>` because the
  inherited 97 tests cover it.
- **F2.** Role vocabulary follows the backend: `Admin` is the administrative role, `User` the
  ordinary staff role. The CRM's `Supervisor`/`Agent` language is retired from the frontend rather
  than renamed in the backend. **Cost:** the delivery plan and `AUTH-n` criteria speak of
  `Supervisor`; those documents now describe `Admin`. Recorded because it is a real vocabulary
  break, not a free substitution.
- **F3.** FluentValidation reports `PropertyName` in **PascalCase** (`"Title"`), while Angular form
  controls are camelCase (`title`). The translation layer lowercases the first character. This is an
  assumption about every current validator, not a guarantee about future ones.
- **F4.** `POST /api/Contents` returns `Result<CreateSuccessDto>`, not the created article, so the
  list must be refetched after create rather than patched from the response.
- **F5.** Fixing the 500 is infrastructure repair, not feature work. It changes configuration and
  wiring only. If it cannot be fixed without changing platform behaviour, the sprint stops and the
  decision returns to the user.

## Out of scope

The ticket workflow UI — no backend exists for it (`BASE-11`–`BASE-14` are unbuilt) · notifications,
platform settings and integration configuration screens · the customer portal beyond its current
scaffold · Playwright end-to-end coverage · a generated OpenAPI client, which is deferred until the
host serves `/openapi/v1.json` · reviewed Arabic copy (`PA-7`).

## Acceptance criteria

Priority: **P0** must hold, **P1** should.

### F0 — The host answers

Prerequisite. No frontend criterion below can be verified live until these hold.

- **FE-1** (P0) `GET /health` on `InternalApi` returns 200 with a healthy body. Evidence is pasted
  command output, not a claim.
- **FE-2** (P0) `GET /openapi/v1.json` returns 200 and a parseable document.
- **FE-3** (P0) `POST /api/Auth/login` with the seeded administrator returns 200 and an
  `accessToken`.
- **FE-4** (P1) The root cause of the 500 is recorded in the plan with the evidence that identified
  it, so the fix is explicable rather than incidental.

### F1 — The contract layer

- **FE-5** (P0) `ApiEnvelope<T>` describes the backend's actual shape: `isSuccess`, `data`, and a
  nullable `error` carrying `code`, `messageAr`, `messageEn`, `type` and `details`.
- **FE-6** (P0) The envelope interceptor unwraps a success envelope to `data` and leaves
  non-enveloped responses untouched.
- **FE-7** (P0) A failure envelope becomes an `ApiError` carrying the code, both localized messages,
  the error type, and field errors derived from `details`.
- **FE-8** (P0) `details` keys are mapped to camelCase control names, so a server error on `Title`
  binds to the `title` control. A key with no matching control surfaces at form level rather than
  being silently dropped.
- **FE-9** (P0) `AuthApi.signIn` calls `POST /api/Auth/login` and returns the real `AuthResponse`
  fields, including both tokens and both expiries.
- **FE-10** (P0) The session store persists the refresh token and the access-token expiry alongside
  the access token.
- **FE-11** (P0) A 401 on any call triggers exactly one refresh attempt via `POST /api/Auth/refresh`;
  on success the original request is retried, on failure the session is cleared and the user is sent
  to the login screen. Concurrent 401s share one refresh, and a failed refresh is never retried.
- **FE-12** (P0) `StaffApi` targets `/api/Users` with `PUT` for activate and deactivate, and its
  types carry `firstName`, `lastName`, `username`, `isActive` and `roles[]`.
- **FE-13** (P0) The staff create form collects email, username, first name, last name, password and
  role, matching `CreateUserDto`.
- **FE-14** (P0) Route guards and navigation use `Admin`, not `Supervisor`. A non-`Admin` user is
  refused the staff route by the guard **and** would be refused by the server if they reached it.
- **FE-15** (P0) The change-password screen is **parked**: it is not routed, not reachable from
  navigation, and its story records `blocked — no backend endpoint`. It is not deleted, and it is
  not reported as shipped.
- **FE-16** (P1) No component references `Supervisor`, `Agent`, `displayName`, `sign-in` or the old
  envelope fields. Verified by search.

### F2 — Knowledge base authoring

The Contents surface, which the baseline maps to brief area 6.

- **FE-17** (P0) The article list calls `GET /api/Contents` with `page` and `pageSize`, renders
  `items` from the `PaginatedList` shape, and shows total count and page controls.
- **FE-18** (P0) The list distinguishes loading, empty and error as visually distinct states.
- **FE-19** (P0) Status and search-term filters reach the server as `status` and `searchTerm` query
  parameters, and clearing a filter refetches without it.
- **FE-20** (P0) An article is created through a validated form. Client rules mirror the server's:
  title required and at most 500 characters, body required, summary at most 1000, content type
  required and at most 50, status one of `Draft`/`Published`/`Archived`.
- **FE-21** (P0) Server validation errors land on the control named by their `details` key, not in a
  banner. This is `FE-8` proven end to end and is the single most valuable integration check here.
- **FE-22** (P0) An existing article is edited through `PUT /api/Contents/{id}` and the list reflects
  the change.
- **FE-23** (P0) The status control offers only transitions the domain permits — `Draft` to
  `Published` or `Archived`, `Published` to `Archived` — and never offers a reversal. `Archived` is
  terminal.
- **FE-24** (P0) Deleting asks for confirmation first and removes the row on success.
- **FE-25** (P1) `authorId` is taken from the signed-in user rather than entered, since
  `CreateContentRequest` requires it and the current user is the author.

## Design

### The anti-corruption layer

The contract difference is absorbed in **one place**: the envelope interceptor. Downstream code —
every component, form and existing test — continues to see unwrapped data or an `ApiError`.

```
HTTP response
   │
   ├─ not an envelope ────────────────────────────────► pass through untouched
   │
   ├─ { isSuccess: true,  data }  ────────────────────► data
   │
   └─ { isSuccess: false, error } ────────────────────► throw ApiError {
            code, messageAr, messageEn, type,
            fieldErrors: details → [{ field: camelCase(key), messages }]
        }
```

Three files carry the whole change: `api-response.ts` (shape), `api-error.ts` (the error object),
`envelope.interceptor.ts` (the mapping). The two API services change their URLs and DTOs. Nothing
else in `common` moves.

This was chosen over mirroring `Result<T>` one-to-one because the dict-shaped `details` binds badly
to reactive-form controls, and over a generated OpenAPI client because `/openapi/v1.json` is one of
the endpoints currently failing. The generated client remains the intended destination once `FE-2`
holds; the interceptor is the seam that makes that swap cheap.

### Refresh

`FE-11` is the one piece of genuinely new behaviour. A single-flight refresh lives beside the auth
interceptor: the first 401 starts a refresh, concurrent 401s wait on that same in-flight call, and a
refresh failure clears the session once. The failure path must not itself trigger a refresh, which
is the classic infinite-loop defect in this pattern and is called out here so the test for it is
written deliberately.

### Layout

```
common/src/lib/
  api/       api-response.ts · api-error.ts · envelope.interceptor.ts   (rewritten)
  auth/      auth.api.ts · staff.api.ts · session.store.ts · auth.interceptor.ts   (re-pointed)
  content/   content.api.ts · content.models.ts                          (new)
admin-app/src/app/features/
  auth/      login.component                                            (re-pointed)
  users/     users.component                                            (re-pointed)
  account/   change-password.component                                  (parked, unrouted)
  content/   content-list · content-form                                (new)
```

New feature screens keep the separate `.ts` / `.html` / `.css` convention the auth-management spec
established. The `common` library keeps its inline templates.

## A finding this spec does not fix

`ContentsController` has **no class-level `[Authorize]`**. `GET /api/Contents` and
`GET /api/Contents/{id}` are anonymous on the *internal* host, and the write actions require only
`[Authorize]` — any authenticated user, with no role check. The `ContentManager` policy is defined
in `AuthorizationExtensions.cs` and applied to nothing.

The frontend cannot correct this; hiding a control is not an authorization control. It is recorded
here so it is not mistaken for an oversight of this spec, and it belongs in the backend's next
security pass.

## Testing

Component and service tests with TestBed and `HttpTestingController`, written test-first per
`CLAUDE.md`. The interceptor's mapping (`FE-5`–`FE-8`) and the refresh single-flight (`FE-11`) are
unit-tested directly, because they are the pieces most likely to be wrong and the least visible when
they are.

`FE-1`–`FE-3` are verified by running the host and pasting the output. Every other criterion is
verified by a test that names it. **No criterion is reported as met without executed output** — the
rule the assessment's Technical Understanding criterion turns on.

Live verification of the re-pointed screens against the running host happens once `F0` holds and is
reported as what it is: a manual smoke check, not an automated test.
