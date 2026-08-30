# US-103 · Every response is traceable to its log line

| Field | Value |
|---|---|
| **Story** | `US-103` *(was `US-1.03`)* |
| **Epic** | [EPIC-12 Platform features](../epics/EPIC-12-platform.md) |
| **Feature** | [`FEAT-01` Platform foundation](../delivery-plan.md#feat-01--platform-foundation) |
| **Layer** | Backend |
| **Ships with** | — Enabler. No user-facing surface, so nothing pairs with it. |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Internal — Support engineer |
| **Priority** | P1 |
| **Sprint** | [1 — Foundation and authentication](../delivery-plan.md#sprint-1--foundation-and-authentication) · Slice S1 |
| **Estimate** | 3 points |
| **Status** | `superseded` |
| **BRD requirements** | FR-11.4, NFR-10 |
| **Spec criteria** | FND-6, FND-7 |
| **Depends on** | — |

## Story

**As a support engineer**, **I want** a correlation id on every response, **so that** I can find the matching server log without asking the caller for a screenshot.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- Every response carries a `traceId` that matches the activity id written to the server log (from
  FND-6).
- `timestamp` is set once, at the API boundary, from the clock abstraction — not by a record
  initialiser, so it is deterministic in tests (from FND-7).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — traceId matches the log line (spec FND-6)

Given any response, when it is emitted, then `traceId` is present on it and matches the activity id
written to the log.

#### AC2 — Timestamp set at the boundary (spec FND-7)

Given the clock abstraction, when a response is composed, then `timestamp` is set once at the API
boundary from the clock abstraction — not by a record initialiser, so it is deterministic in tests.

## SQL tables

None — this story touches no persisted data.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | FND-7 | Application.Tests | ✅ `ResponseTests.Timestamp_Is_Default_Until_Stamped_At_The_Boundary` | a freshly built response / inspect `Timestamp` / — | default value — the record does not stamp itself |
| TC-02 | FND-7 | Api.IntegrationTests | ✅ `Health_Returns_The_Standard_Envelope` (timestamp assertion) | composed API / `GET /api/health` / inspect | non-default `timestamp`, set at the boundary |
| TC-03 | FND-6 | Api.IntegrationTests | ✅ `Property_Names_Are_CamelCase` (presence only) | any response / inspect payload / — | `traceId` key present, camelCase |
| TC-04 | FND-6 | Api.IntegrationTests | `planned` — the correlation half is what makes this `partial` | a request with its server log captured / respond / compare | response `traceId` equals the logged activity id |

## Notes

Stamping the timestamp in a record initialiser is the obvious implementation and it makes every assertion about the field untestable, because the value changes between constructing the expected object and comparing it.

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
