# US-111 · The API documents itself truthfully and reports its health

| Field | Value |
|---|---|
| **Story** | `US-111` *(was `US-1.11`)* |
| **Epic** | [EPIC-12 Platform features](../epics/EPIC-12-platform.md) |
| **Feature** | [`FEAT-01` Platform foundation](../delivery-plan.md#feat-01--platform-foundation) |
| **Layer** | Backend |
| **Ships with** | — Enabler. No user-facing surface, so nothing pairs with it. |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Internal — Integration owner |
| **Priority** | P1 |
| **Sprint** | [1 — Foundation and authentication](../delivery-plan.md#sprint-1--foundation-and-authentication) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `superseded` |
| **BRD requirements** | FR-11.2 |
| **Spec criteria** | FND-30, FND-31, FND-32 |
| **Depends on** | [US-101](./US-101-uniform-response-envelope.md), [US-102](./US-102-outcome-to-status-mapping.md) |

## Story

**As an integration owner**, **I want** published documentation that describes the actual response shape, and a health endpoint that exercises the pipeline, **so that** I can code against the API without reading its source.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- The OpenAPI document describes the envelope truthfully — endpoints declare the wrapped shape they
  actually return, not the bare payload (from FND-30).
- The documentation UI serves the document and can execute an authenticated request (from FND-31).
- A health endpoint returns the standard envelope, proving the whole pipeline is composed (from
  FND-32).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — OpenAPI describes the envelope truthfully (spec FND-30)

Given published documentation, when generated, then endpoints declare the wrapped shape they
actually return, not the bare payload.

#### AC2 — UI serves and executes requests (spec FND-31)

Given the documentation UI, when used, then it serves the document and can execute an authenticated
request.

#### AC3 — Health proves the composed pipeline (spec FND-32)

Given the health endpoint, when called, then it returns the standard envelope, proving the whole
pipeline is composed.

## SQL tables

None — this story touches no persisted data.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | FND-32 | Api.IntegrationTests | ✅ `HealthEndpointTests.Health_Returns_The_Standard_Envelope` | composed pipeline / `GET /api/health` / inspect | standard envelope — catalogue, clock, mapping, middleware all proven |
| TC-02 | FND-30 | Api.IntegrationTests | ✅ `OpenApi_Document_Is_Served` | composed API / fetch the OpenAPI document / inspect | document served; `/api/health` present |
| TC-03 | FND-30 | Api.IntegrationTests | `planned` | an endpoint's declared response schema / compare against `Response<T>` / — | published schema matches the envelope actually returned |
| TC-04 | FND-31 | Api.IntegrationTests | `planned` — blocked on US-112 (needs authentication to exist) | Scalar UI / execute a request with a token / observe | document served and authenticated execution succeeds |

## Notes

The first criterion is the difference between documentation and decoration. Generated documentation that declares the unwrapped payload is worse than none, because a consumer will believe it and build against a shape the server never sends.

The health endpoint earns its place by being the cheapest possible end-to-end proof: if it returns the envelope with a code, a message in both languages and a traceId, then the catalogue loaded, the clock is wired, the mapping is registered and the middleware is in the pipeline. That is also why its failure blocks US-101 and US-103 from reaching `done`.

## Open questions

None.

## Status evidence

**Superseded 2026-08-25.** The code that satisfied this story was replaced when the CCE Platform
reference was adopted as the CRM baseline ([ADR-0009](../../adr/0009-adopt-the-support-platform-as-the-crm-baseline.md)).

The criterion it cites is still a valid **requirement**. What is no longer true is that this
codebase meets it: the implementation named in the previous evidence is archived, not running. The
adopted platform may satisfy the same intent by different means, but that has **not been
re-verified**, and carrying a `done` for code that no longer exists would be the exact false claim
this file exists to prevent.

Re-verify against the new baseline, or re-scope the story to the platform equivalent.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
