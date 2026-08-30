# FEAT-09 — Contract hardening · task record

**Plan:** [`implementation-plan.md`](./implementation-plan.md)
**Executed:** 2026-08-26
**Status:** delivered — `AC-51`, `AC-52`, `AC-53`, `AC-54` met; **`AC-66` knowingly not met** (ADR-0013)

## Evidence

```
dotnet build CustomerSupport.slnx  → Build succeeded, 0 errors
dotnet test  CustomerSupport.slnx
Passed!  - Failed: 0, Passed: 242, Skipped: 0, Total: 242, Duration: 59 s

npx ng test common    --watch=false → 55 passed (14 files)
npx ng test admin-app --watch=false → 49 passed (9 files)
```

## Tasks

| # | Task | Criteria | Commit | Status |
|---|---|---|---|---|
| [01](./tasks/task-01-the-audit.md) | Write the audit, run it before any fix | AC-51…AC-54 | uncommitted | `done` |
| [02](./tasks/task-02-close-the-findings.md) | Close the four defects it found | AC-51, AC-53, AC-54 | uncommitted | `done` |

## What the audit found

Written and run **before** a line was changed. Three of nine assertions failed, and every failure
was a real defect that had been shipping since the baseline:

| Finding | Criterion | Impact |
|---|---|---|
| `api/Users` and `api/externalapi-configs` returned an **empty body** on 403 | `AC-51` | Every deliberate refusal — including `AC-43`'s supervisor-only assign — answered with nothing a client could render |
| `api/Health` returned a bare object, outside the envelope | `AC-51` | A consumer needs a special case for one route |
| **No trace identifier anywhere** — not in the envelope, not in a header | `AC-53` | Criterion entirely unmet. The frontend had hardcoded `traceId: ''` with a comment saying the backend sent none |
| `createdAt` = `"2026-08-25T22:58:48.9296923"` — **no timezone designator** | `AC-54` | Every browser parses UTC as local. An agent in Cairo saw every timestamp shifted three hours, silently |

Three assertions passed and are worth naming because they could easily have failed:
`AC52_AFailingRequest_LeaksNoInternals`, `AC52_UnknownRoute_ReturnsTheEnvelopeNotAnHtmlPage`, and
`EveryErrorCode_HasABilingualMessage` — **131 codes declared, 0 without a catalogue entry**.

## Criteria delivered

| `AC-n` | Test naming it | Outcome |
|---|---|---|
| AC-51 | `AC51_EveryEndpoint_AnswersInTheEnvelope` (14 routes), `AC51_EveryFailure_CarriesACodeAndBothLanguages` | met |
| AC-52 | `AC52_AFailingRequest_LeaksNoInternals`, `AC52_UnknownRoute_…`, `AC52_MalformedJson_…` | met |
| AC-53 | `AC53_Responses_CarryATraceIdentifier` | met — after F3 |
| AC-54 | `AC54_ResponseProperties_AreCamelCase`, `AC54_DatesOnTheWire_AreIso8601Utc` | met — after F4 |
| AC-66 | none | **not met** — [ADR-0013](../../../adr/0013-named-error-codes-over-ac66-numbering.md) |

## Deviations from the plan

**D1 — The plan put the new middleware in the wrong place, and the plan was wrong.**
It said "registered immediately after `UseAuthorization()`". That cannot work: authorization
short-circuits the pipeline, so everything downstream of the short circuit is skipped and the
middleware never ran. The 403s stayed bodiless through a full build-and-test cycle.

It has to sit **upstream** of `UseAuthentication`/`UseAuthorization` so it wraps them and can inspect
the status on the way back out. The corrected reasoning is in the file, because "after authorization"
is the intuitive and wrong answer.

**D2 — `AC-66` produced an ADR rather than code.**
Planned as a decision task and delivered as one. The temptation was to rename the eight codes the
criterion names; that would leave eight opaque `ERRnnn` beside 123 named ones, which is worse than
either consistent choice.

## Known gaps this feature did not close

- **`AC-66`** — named codes, not `ERRnnn`. Recorded above and in traceability.
- **`AC-53` is envelope-only.** There is no `X-Trace-Id` response header, so a caller reading a
  network log without parsing the body still cannot quote an id. The criterion says the response
  carries it, which it now does; the header would be a courtesy and is not built.
- The audit inspects **parameterless GET routes** only — 14 of them. Routes with parameters need
  fixtures and are covered by the feature suites instead. A POST-only route added tomorrow would not
  be swept by `AC51_EveryEndpoint_AnswersInTheEnvelope`.
