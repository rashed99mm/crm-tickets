# US-122 · One envelope and one stable code per condition

| Field | Value |
|---|---|
| **Story** | `US-122` *(was `US-1.33`)* |
| **Epic** | [EPIC-12 Platform features](../epics/EPIC-12-platform.md) |
| **Feature** | [`FEAT-09` Contract hardening](../delivery-plan.md#feat-09--contract-hardening) |
| **Layer** | Backend |
| **Ships with** | — API-only. Cross-cutting hardening proven across the endpoints the earlier features built. |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Internal — API consumer |
| **Priority** | P0 |
| **Sprint** | [4 — Contract hardening, localisation and the journey](../delivery-plan.md#sprint-4--contract-hardening-localisation-and-the-journey) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `partial` |
| **BRD requirements** | FR-11.1, INT-3, INT-4 |
| **Spec criteria** | AC-51, AC-66 |
| **Depends on** | [US-101](./US-101-uniform-response-envelope.md) *(sprint 1)* |

## Story

**As an API consumer**, **I want** a stable machine-readable code on every response, **so that** I branch on the code rather than parsing prose.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- Every response is the envelope `{ success, code, message: { ar, en }, data, errors[], traceId,
  timestamp }`; validation failures carry top-level `VAL001` plus one `errors[]` entry per field
  (from AC-51).
- Each condition named throughout the spec carries its documented code — duplicate email, delete
  guard, invalid transition, self-transition, concurrency conflict, ownership refusal, oversized
  upload (413) and disallowed type (415) (from AC-66).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Envelope on every response (spec AC-51)

Every response — success and failure — is the envelope `{ success, code, message: { ar, en }, data,
errors[], traceId, timestamp }`. A validation failure carries top-level `VAL001` and one `errors[]`
entry per field.

#### AC2 — Documented codes per condition (spec AC-66)

The conditions named throughout the spec carry their documented codes: duplicate email, delete
guard, invalid transition, self-transition, concurrency conflict, ownership refusal, oversized
upload (413) and disallowed type (415).

## SQL tables

None — this story is the wire contract over data written by other stories.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-51 | Api.IntegrationTests | `planned` | each endpoint exercised once successfully / inspect body | exact envelope shape, both languages |
| TC-02 | AC-51 | Api.IntegrationTests | `planned` | an invalid create / inspect | top-level `VAL001` + one `errors[]` entry per field |
| TC-03 | AC-66 | Application.Tests | ✅ partially — `SystemCodeMapTests.Maps_Domain_Key_To_Its_Code_And_Type` + `Attachment_Codes_Use_The_Payload_And_Media_Type_Statuses` prove the map half today | each documented condition (duplicate email → `ERR011`, delete guard → `ERR012`, invalid/self transition → `ERR021`/`ERR022`, concurrency → `ERR024`, ownership → `ERR023`, 413/415 → `ERR051`/`ERR052`) / trigger / inspect `code` | exactly the documented code per condition |
| TC-04 | AC-66 | Api.IntegrationTests | `planned` — endpoint-level proof of TC-03's table | same conditions via HTTP / inspect bodies | codes survive the boundary |

## Notes

Codes are permanent from here. A code's meaning never changes; a new meaning gets a new code. An integration consumer who cannot rely on that has to re-test on every release, and will eventually stop upgrading.

## Open questions

None.

## Status evidence

Implemented as a cross-cutting audit plus the fixes it found.

AC-51 -> `AC51_EveryEndpoint_AnswersInTheEnvelope` (14 parameterless GET routes) and
`AC51_EveryFailure_CarriesACodeAndBothLanguages`. Plus `EveryErrorCode_HasABilingualMessage` -
**131 codes declared, 0 without a catalogue entry**.

The audit found two routes returning an **empty body on 403** (authorization short-circuits before
the exception middleware) and `api/Health` answering outside the envelope. Both fixed:
`AuthorizationEnvelopeMiddleware` and a rewritten `HealthController`.

**AC-66 is NOT met.** The platform emits named codes (`CUSTOMER_EMAIL_EXISTS`,
`TICKET_TRANSITION_NOT_ALLOWED`) rather than the `ERRnnn` numbering the criterion names. Settled by
[ADR-0013](../../adr/0013-named-error-codes-over-ac66-numbering.md): the intent (one stable
documented code per condition) holds, the literal text does not, and the spec should be amended.
Recorded as a gap, not argued away. **This story is `partial` for that reason.**

Run 2026-08-26: `dotnet test` - 242 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
