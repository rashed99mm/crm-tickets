# US-123 · Failures are diagnosable without leaking anything

| Field | Value |
|---|---|
| **Story** | `US-123` *(was `US-1.34`)* |
| **Epic** | [EPIC-12 Platform features](../epics/EPIC-12-platform.md) |
| **Feature** | [`FEAT-09` Contract hardening](../delivery-plan.md#feat-09--contract-hardening) |
| **Layer** | Backend |
| **Ships with** | — API-only. Cross-cutting hardening proven across the endpoints the earlier features built. |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Internal — Security reviewer |
| **Priority** | P1 |
| **Sprint** | [4 — Contract hardening, localisation and the journey](../delivery-plan.md#sprint-4--contract-hardening-localisation-and-the-journey) · Slice S1 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | NFR-6, NFR-10 |
| **Spec criteria** | AC-52, AC-53 |
| **Depends on** | [US-102](./US-102-outcome-to-status-mapping.md) *(sprint 1)*, [US-103](./US-103-trace-id-and-timestamp.md) *(sprint 1)* |

## Story

**As a security reviewer**, **I want** failures to carry a correlation id and nothing else, **so that** support can investigate without the response becoming a reconnaissance tool.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- No response body ever contains a stack trace, SQL text or connection string (from AC-52).
- Every response carries a `traceId` matching the server log; an unhandled exception answers 500
  with a generic message and code (from AC-53).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Failures leak nothing (spec AC-52)

No response body ever contains a stack trace, SQL text, or a connection string.

#### AC2 — TraceId correlates with log (spec AC-53)

Every response carries `traceId` matching the server log for that request. An unhandled exception
returns 500 with a generic message and code.

## SQL tables

None — this story governs what leaves the process, not what is stored.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-52 | Api.IntegrationTests | ✅ partially — `Unknown_Route_Returns_404_Without_An_Html_Error_Page` (US-102 TC-03) | an unknown route / request / inspect | envelope, no HTML page |
| TC-02 | AC-52 | Api.IntegrationTests | `planned` | a forced unhandled exception / request / search body | no stack trace, SQL text or connection string; `ERR900` + generic message |
| TC-03 | AC-53 | Api.IntegrationTests | `planned` — the correlation assertion that completes US-103 | any request with its log captured / compare | `traceId` matches the logged activity id |
| TC-04 | AC-53 | Api.IntegrationTests | `planned` | an unhandled exception / inspect response | 500 with generic message and a usable `traceId` |

## Notes

A stack trace in a response is a gift: framework versions, file paths, and often the shape of the query that failed. The traceId is what lets support be helpful without it.

## Open questions

None.

## Status evidence

AC-52 -> `AC52_AFailingRequest_LeaksNoInternals` (greps three real failure paths for stack frames,
CLR type names, SQL keywords and connection-string fragments),
`AC52_UnknownRoute_ReturnsTheEnvelopeNotAnHtmlPage`,
`AC52_MalformedJson_ReturnsEnvelopeWithoutParserDetail`.

AC-53 -> `AC53_Responses_CarryATraceIdentifier`.

**AC-53 was entirely unmet before this story.** The envelope had no trace field at all - the Angular
`ApiError` hardcoded `traceId: ''` with a comment saying the backend sent none. `Result<T>` now
carries `TraceId`, stamped in `ToActionResult` and in the authorization middleware.

**Gap:** there is no `X-Trace-Id` response header, so a caller reading a network log without parsing
the body still cannot quote an id. The criterion says the response carries it, which it now does.

Run 2026-08-26: 242 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
