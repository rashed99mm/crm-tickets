# FEAT-09 — Contract hardening · backend plan

> **Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.**

**Date:** 2026-08-26
**Feature:** `FEAT-09`, sprint 4, **API-only** · 3 stories · 10 points
**Spec:** `AC-51`, `AC-52`, `AC-53`, `AC-54`, `AC-66`
**Depends on:** `FEAT-02`…`FEAT-08` — this is the pass that *proves* criteria the earlier features
were each expected to satisfy, and it can only run once the surface exists

## What this feature is

These criteria are **continuous obligations**, not late work. `FEAT-09` does not introduce the
behaviour; it audits the whole surface at once and turns "we believe every endpoint does this" into
a test that fails when one stops.

**The audit was written first and run before any fix.** It found three defects, listed below with
the code that closes each. Everything in the *Code plan* section exists because the audit failed,
not because it was guessed at.

## Audit results — run 2026-08-26, before any change

```
PASS  AC51_EveryFailure_CarriesACodeAndBothLanguages
PASS  EveryErrorCode_HasABilingualMessage          131 codes declared, 0 without a catalogue entry
PASS  AC52_AFailingRequest_LeaksNoInternals
PASS  AC52_UnknownRoute_ReturnsTheEnvelopeNotAnHtmlPage
PASS  AC52_MalformedJson_ReturnsEnvelopeWithoutParserDetail
PASS  AC54_ResponseProperties_AreCamelCase

FAIL  AC51_EveryEndpoint_AnswersInTheEnvelope       3 of 14 GET routes
FAIL  AC53_Responses_CarryATraceIdentifier          no traceId, no correlation header
FAIL  AC54_DatesOnTheWire_AreIso8601Utc             "2026-08-25T22:58:48.9296923"
```

---

## Code plan

### F1 — `AC-51`: a 401 or 403 returns an empty body

```
api/Users                   -> empty body (403)
api/externalapi-configs     -> empty body (403)
```

ASP.NET's authorization middleware short-circuits **before** the exception middleware and writes a
bare status with no content. Every deliberate refusal in the product — including `AC-43`'s
supervisor-only assign — therefore answers with nothing a client can render.

**New file:** `src/CustomerSupport.Api.Shared/Middleware/AuthorizationEnvelopeMiddleware.cs`

```csharp
// Sits AFTER UseAuthorization. Inspects the status the pipeline settled on and, if it is a
// bodiless 401/403, writes the standard envelope instead.
public sealed class AuthorizationEnvelopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IMessageFactory messageFactory)
    {
        var originalBody = context.Response.Body;          // buffer so we can tell "no body written"
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        var isBodiless = buffer.Length == 0;
        var isRefusal  = context.Response.StatusCode is 401 or 403;

        if (isRefusal && isBodiless && !context.Response.HasStarted)
        {
            var code = context.Response.StatusCode == 401
                ? ApplicationErrors.General.UNAUTHORIZED
                : ApplicationErrors.General.FORBIDDEN;
            await WriteEnvelopeAsync(context, messageFactory);
            return;
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody);
    }
}
```

The shipped `WriteEnvelopeAsync` builds the envelope with `messageFactory.Fail<Unit>(code, type)`
and stamps `traceId` directly off `Activity.Current?.Id ?? context.TraceIdentifier` inside the same
anonymous envelope object — so a refused request is quotable too, matching F3's `Result.TraceId` for
handled responses rather than duplicating the work.

Registered in `WebApplicationExtensions.UsePlatformPipeline` immediately after `UseAuthorization()`.

**Why a middleware and not a `StatusCodePages` handler:** `UseStatusCodePages` re-executes the
pipeline for a status it did not produce, and would also fire for statuses this project sets
deliberately with a body already written. Buffering and checking "did anything get written" is the
narrower question.

### F2 — `AC-51`: `api/Health` answers outside the envelope

```json
{"status":"healthy","timestamp":"..."}
```

**Edit:** `src/CustomerSupport.InternalApi/Controllers/HealthController.cs` — return
`Result<HealthDto>` through `this.ToActionResult(...)` like every other controller.

**Deliberately left alone:** the minimal-API `GET /health` and `/health/ready` registered in
`MapPlatformEndpoints`. Those are **probe** endpoints for orchestrators, which expect
`AspNetCore.HealthChecks`' own shape; wrapping them would break the thing that consumes them. The
audit only inspects `api/*` routes, and this exemption is recorded rather than assumed.

### F3 — `AC-53`: no trace identifier anywhere

The envelope has no `traceId` field at all — `Result<T>` is `{ IsSuccess, Data, Error }`. The
frontend's `ApiError` already carries `traceId: ''` with a comment saying the backend does not send
one, so this has been a known hole since the frontend was written.

**Edit:** `src/CustomerSupport.Application/Contracts/Result.cs`

```csharp
public record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public Error? Error { get; init; }

    /// The current Activity's id, stamped on the way out so a caller can quote it to support.
    /// Init-only and never set by a handler: it is a transport concern, and a handler that could
    /// set it could also forge it.
    public string? TraceId { get; init; }
}
```

**Edit:** `ResultActionResultExtensions.ToActionResult` — stamp it in the one place every response
already funnels through:

```csharp
result = result with { TraceId = Activity.Current?.Id ?? controller.HttpContext.TraceIdentifier };
```

**Edit:** the exception middleware and `AuthorizationEnvelopeMiddleware` — same stamp, so a 500 and
a 403 are quotable too.

**Frontend follow-on:** `envelope.interceptor.ts` currently hardcodes `''`; it reads
`envelope.traceId` once this ships, and `CsErrorState` already renders a trace id when present.

### F4 — `AC-54`: dates carry no timezone designator

`"2026-08-25T22:58:48.9296923"` parses as **local** time in every browser. Entities store
`DateTime.UtcNow`, but EF returns `DateTimeKind.Unspecified` after a round trip, so
`System.Text.Json` writes no `Z`.

**New file:** `src/CustomerSupport.Api.Shared/Serialization/UtcDateTimeConverter.cs`

```csharp
/// Every DateTime this system stores is UTC. After an EF round trip Kind is Unspecified, so the
/// serializer omits the designator and the value silently becomes local on the client. This
/// asserts what the schema already guarantees.
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type _, JsonSerializerOptions __) =>
        DateTime.SpecifyKind(reader.GetDateTime(), DateTimeKind.Utc);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions _) =>
        writer.WriteStringValue(
            (value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime())
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
}
```

Plus a `UtcNullableDateTimeConverter` for `DateTime?`. Registered in
`WebApiServiceExtensions` alongside the existing `JsonStringEnumConverter`.

**Why a converter rather than an EF value converter:** an EF converter would fix reads through EF
but not values a handler computes in memory, and it would touch every entity. The wire format is a
serialization concern and belongs at the serializer.

### F5 — `AC-66`: the code vocabulary conflict, settled

`AC-66` names `ERR011`, `ERR012`, `ERR021`, `ERR022`, `ERR023`, `ERR024`, `ERR051`, `ERR052`. The
platform emits named codes and has since the baseline was adopted.

**No code change.** This is settled by **ADR-0013**, which records that the named codes are the
delivered contract, that `AC-66` is **not literally met**, and proposes the amendment. Renaming 131
codes to opaque numbers to satisfy the letter of one criterion would make the contract worse, and
declaring the two "equivalent" without saying so would be the failure `CLAUDE.md`'s AI-usage rules
exist to prevent.

`rubric-traceability.md` records it as a gap, not as met.

---

## Tasks

| # | Task | Criteria | Test that must fail first |
|---|---|---|---|
| 1 | The audit suite itself, written and run before any fix | all | the six listed above |
| 2 | F1 + F2 — envelope on refusals and on `api/Health` | `AC-51` | `AC51_EveryEndpoint_AnswersInTheEnvelope` |
| 3 | F3 — `traceId` through the envelope | `AC-53` | `AC53_Responses_CarryATraceIdentifier` |
| 4 | F4 — UTC designator on every date | `AC-54` | `AC54_DatesOnTheWire_AreIso8601Utc` |
| 5 | F5 — ADR-0013 and the traceability entry | `AC-66` | none — a decision, not code |

## Definition of done

1. `AC-51`, `AC-52`, `AC-53`, `AC-54` each covered by a passing test naming it.
2. `AC-66` resolved by ADR and recorded as a gap in traceability.
3. Frontend still green after the envelope gains a field.
4. Suite green, output pasted.
5. Task records in `tasks/`.
