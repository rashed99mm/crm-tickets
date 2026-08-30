# ADR 0004 — Unified response envelope instead of RFC 9457 ProblemDetails

- **Status:** Accepted
- **Date:** 2026-08-24

## Context

Every endpoint needs one response contract. The S1 spec originally specified RFC 9457
`ProblemDetails` for failures, which ASP.NET Core produces natively.

Two requirements arrived afterwards that `ProblemDetails` does not serve well:

1. **Success needs a code and a message too.** `ProblemDetails` is a failure format by definition.
   With it, a successful creation returns data and nothing else, so the frontend hardcodes its own
   confirmation text and the server's message catalogue only covers half the application's output.
2. **Both languages in every response.** The client picks `ar` or `en` and can switch without
   refetching. `ProblemDetails` carries a single `detail` string, so this would mean either
   server-side content negotiation or a non-standard extension.

A house pattern already exists for this in the CustomerSupport platform, and consistency across the
organisation's codebases has value of its own.

## Decision

Every endpoint returns:

```json
{ "success": false, "code": "ERR010",
  "message": { "ar": "...", "en": "..." },
  "data": null, "errors": [], "traceId": "00-...", "timestamp": "..." }
```

**HTTP status codes stay meaningful** — 404, 409, 413, 422 and so on, driven by a `MessageType`
enum mapped in exactly one place. The envelope is carried *in the body of a correctly-statused
response*, not as a 200 wrapping a failure.

## Alternatives considered

| Option | Why it lost |
|---|---|
| **`ProblemDetails` with `errorCode` and `referenceCode` extensions** — the recommended option | Standards-compliant, works with generated OpenAPI clients out of the box, and extensions could have carried the codes. It lost on the two requirements above: it has nothing to say about success, and a single `detail` string cannot carry two languages without a non-standard shape. At which point the standard is being bent rather than followed. |
| **Envelope with `200 OK` for everything, `success: false` in the body** | The common failure mode of envelope APIs. Breaks HTTP caching and proxies, makes every client inspect the body to know whether the call worked, and discards the one piece of error signalling every tool already understands. Explicitly rejected. |
| **Envelope for success, `ProblemDetails` for failure** | Two contracts wearing one name. Every client branches on which shape arrived, which is worse than either alone. |

## Consequences

- The frontend has one shape to handle, with a code it can `switch` on and a message it can
  display without owning any copy.
- Support can quote a `traceId` that leads to the exact log line.
- **The cost is real:** this is a non-standard contract. Generated OpenAPI clients model it as an
  ordinary schema rather than as errors, so tooling that special-cases `ProblemDetails` gains
  nothing here. Anyone joining the project has to learn the envelope.
- `MessageType` → status lives in one mapper. If that mapping is wrong, it is wrong everywhere at
  once — which is preferable to being wrong in scattered places, but it makes that function worth
  testing directly.
- Adding a third language changes the response shape, because the languages are keys rather than
  a negotiated value. That is a known limit of choosing F2 over content negotiation.
- This decision was taken against the recommendation on this record. The reasoning above is the
  case *for* it, and it is a legitimate one; the trade-off is documented so it can be revisited
  rather than rediscovered.
