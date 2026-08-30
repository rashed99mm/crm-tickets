# Task 1 — Write the audit, and run it before fixing anything

| Field | Value |
|---|---|
| Plan | [`implementation-plan.md`](../implementation-plan.md) |
| Feature | `FEAT-09` Contract hardening |
| Criteria | `AC-51`, `AC-52`, `AC-53`, `AC-54` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `tests/CustomerSupport.Tests/Integration/ContractHardeningTests.cs`

## Test evidence — the first run, before any change

```
PASS  AC51_EveryFailure_CarriesACodeAndBothLanguages
PASS  EveryErrorCode_HasABilingualMessage        131 codes declared, 0 without a catalogue entry
PASS  AC52_AFailingRequest_LeaksNoInternals
PASS  AC52_UnknownRoute_ReturnsTheEnvelopeNotAnHtmlPage
PASS  AC52_MalformedJson_ReturnsEnvelopeWithoutParserDetail
PASS  AC54_ResponseProperties_AreCamelCase

FAIL  AC51_EveryEndpoint_AnswersInTheEnvelope    api/Users -> empty body (403)
                                                 api/externalapi-configs -> empty body (403)
                                                 api/Health -> not an envelope
FAIL  AC53_Responses_CarryATraceIdentifier       traceId in envelope: False; header: False
FAIL  AC54_DatesOnTheWire_AreIso8601Utc          "2026-08-25T22:58:48.9296923"
```

## Why the order mattered

Writing the audit first is the whole method here. Every one of these criteria was *believed* to hold
— eight features had shipped against them and nobody had reported a problem. Three did not, and two
of the three had been broken since the platform was adopted.

Had the fixes been written first and the tests after, the tests would have been shaped around the
fixed behaviour and the pre-existing defects would never have been visible as defects. The failing
output above is the artefact worth keeping.

## Two assertions written to fail, and why they were written that way

**`AC53_Responses_CarryATraceIdentifier`** was written with the expectation of failure already
recorded in the plan: the Angular `ApiError` carried `traceId: ''` with a comment saying the backend
sent none. The test could have been written to accept a missing trace id "for now". Writing it
strictly is what turned a known-but-tolerated hole into a fixed one.

**`AC54_DatesOnTheWire_AreIso8601Utc`** came from reading a live response by hand while the app was
running, not from reading code. `2026-08-25T22:16:20.9707248` looks like ISO 8601 and is — it just
has no offset, which no unit test would notice and every browser gets wrong.

## Deviations from the plan

**1. No test-only throwing endpoint.**
The plan wanted a forced unhandled exception for `AC-52`. Adding an endpoint that throws means
adding a route that ships. `AC52_AFailingRequest_LeaksNoInternals` drives three real failure paths
instead and greps the bodies for stack frames, CLR type names, SQL keywords and connection-string
fragments.

**2. `EveryErrorCode_HasABilingualMessage` reads the constants, not the responses.**
Reflecting over `ApplicationErrors` and checking each code against `Resources.yaml` covers all 131,
including codes no test happens to trigger. This is the test that would have caught the missing-YAML
defect found by running the app — where the catalogue never reached the output directory and every
message silently became its own key.

## Scope of the sweep, stated plainly

`AC51_EveryEndpoint_AnswersInTheEnvelope` enumerates `EndpointDataSource` and drives every
**parameterless GET** route under `api/` — 14 of them. Routes with parameters need fixtures and are
covered by the feature suites. **A POST-only route added tomorrow is not swept**, and that limit is
recorded rather than left for someone to assume otherwise.
