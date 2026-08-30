# Task 2 — Close the four defects the audit found

| Field | Value |
|---|---|
| Plan | [`implementation-plan.md`](../implementation-plan.md) — F1…F4 |
| Feature | `FEAT-09` Contract hardening |
| Criteria | `AC-51`, `AC-53`, `AC-54` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- **new** `src/CustomerSupport.Api.Shared/Middleware/AuthorizationEnvelopeMiddleware.cs`
- **new** `src/CustomerSupport.Api.Shared/Serialization/UtcDateTimeConverter.cs`
- `src/CustomerSupport.Application/Contracts/Result.cs` (`TraceId`)
- `src/CustomerSupport.Api.Shared/Extensions/ResultActionResultExtensions.cs`
- `src/CustomerSupport.Api.Shared/Extensions/WebApplicationExtensions.cs`
- `src/CustomerSupport.Api.Shared/Extensions/WebApiServiceExtensions.cs`
- `src/CustomerSupport.InternalApi/Controllers/HealthController.cs`
- `frontend/projects/common/src/lib/api/{api-response.ts,envelope.interceptor.ts}`

## Test evidence — after

```
dotnet test CustomerSupport.slnx
Passed!  - Failed: 0, Passed: 242, Skipped: 0, Total: 242

traceId in envelope: True
createdAt on the wire: 2026-08-25T23:03:42.478Z
audited 14 parameterless GET routes   (no offenders)

npx ng test common    → 55 passed
npx ng test admin-app → 49 passed
```

## The fix that took two attempts

**`AuthorizationEnvelopeMiddleware` was registered in the wrong place, and the plan said to put it
there.** The plan specified "immediately after `UseAuthorization()`", which is the intuitive answer
and cannot work: authorization **short-circuits** the pipeline, so nothing downstream of the short
circuit executes. The middleware never ran, and the 403s stayed bodiless through a full
build-and-test cycle.

It has to sit **upstream** of `UseAuthentication`/`UseAuthorization` so that it wraps them, and can
inspect the settled status on the way back out. Corrected, with the reasoning in the file so the
next reader does not "fix" it back.

The middleware buffers the response body and asks a narrow question — *did anything get written?* —
because a 403 that already carries a body (one a handler produced deliberately) must pass through
untouched. `UseStatusCodePages` was rejected: it re-executes the pipeline for a status it did not
produce.

## The one with real user impact

**`UtcDateTimeConverter`.** `createdAt` was going out as `2026-08-25T22:58:48.9296923` — ISO 8601 in
shape, with **no timezone designator**. Entities store `DateTime.UtcNow`, but EF returns
`DateTimeKind.Unspecified` after a round trip, so `System.Text.Json` writes no `Z`.

Every browser parses that as local time. On this machine that is invisible; for an agent in Cairo
every timestamp on every ticket and every history row was **three hours off**, silently, with
nothing in the UI to suggest it.

A serialization converter rather than an EF value converter: an EF converter fixes values read
through EF but not ones a handler computes, and it would have to be applied per entity. The nullable
overload is separate and necessary — a `DateTime?` bypasses the non-nullable converter entirely.

## `traceId`, and the contract change it implies

`Result<T>` gained `TraceId`, stamped in `ToActionResult` — the single method every controller
response funnels through, so no path can forget it — and in the new middleware, so a refusal is
quotable too.

It is `init`-only and never set by a handler. A handler able to set it would be a handler able to
forge it, and the value is only useful if it correlates with the server's own log.

**Frontend follow-on:** `envelope.interceptor.ts` had hardcoded `''` with a comment explaining the
backend sent none. It now reads `envelope.traceId`, and `CsErrorState` — which has always rendered a
trace id when present, forced LTR so it stays readable in Arabic — finally has one to show.

## Deviations from the plan

**1. Middleware placement, as above.** The plan was wrong; the record says so rather than quietly
shipping the corrected version.

**2. `api/Health` was rewritten rather than exempted.** The two minimal-API probe endpoints
(`/health`, `/health/ready`) *are* exempt, deliberately: orchestrators and health-check tooling
expect their shape. `api/Health` is part of the documented API surface and a consumer should not
need a special case for one route.
