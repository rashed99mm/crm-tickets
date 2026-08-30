# US-101 · Uniform response envelope

| Field | Value |
|---|---|
| **Story** | `US-101` *(was `US-1.01`)* |
| **Epic** | [EPIC-12 Platform features](../epics/EPIC-12-platform.md) |
| **Feature** | [`FEAT-01` Platform foundation](../delivery-plan.md#feat-01--platform-foundation) |
| **Layer** | Backend |
| **Ships with** | — Enabler. No user-facing surface, so nothing pairs with it. |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Internal — API consumer |
| **Priority** | P0 |
| **Sprint** | [1 — Foundation and authentication](../delivery-plan.md#sprint-1--foundation-and-authentication) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `superseded` |
| **BRD requirements** | FR-11.1, FR-12.2 |
| **Spec criteria** | FND-1, FND-2, FND-3, FND-5 |
| **Depends on** | — |

## Story

**As an API consumer**, **I want** every response to have one predictable shape, **so that** I write
one handler instead of one per endpoint.

## Business rules

No `BR-n` in the BRD covers the envelope directly (ADR 0004 records the decision). Derived from the
cited criteria:

- Every response carries a machine-readable system code and a message in both Arabic and English —
  no endpoint answers in prose alone (from FND-1, FND-2, FND-3).
- An operation that would conventionally return 204 returns 200 with the envelope instead — no
  response exists without a code and a message (from FND-5).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — One predictable shape (spec FND-1)

Given any endpoint, when it responds, then the body is
`{ success, code, message: { ar, en }, data, errors[], traceId, timestamp }`.

#### AC2 — Success carries its confirmation (spec FND-2)

Given a successful operation, then `success` is true, the code is a `CON` code, and the message is
non-empty in both languages.

#### AC3 — Failure carries its error (spec FND-3)

Given a failed operation, then `success` is false, the code is an `ERR` or `VAL` code, and the
message is non-empty in both languages.

#### AC4 — No body-less success (spec FND-5)

Given an operation that would conventionally return 204, when it succeeds, then it returns 200 with
the envelope instead.

## SQL tables

None — no persisted data. The envelope is composed at the API boundary.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | FND-1 | Api.IntegrationTests | ✅ `Health_Returns_The_Standard_Envelope` | GET /health / inspect body | all seven envelope members present, code `CON900`, both languages, empty errors, non-default timestamp |
| TC-02 | FND-1, FND-2, FND-3 | Application.Tests | ✅ `ResponseTests` (8 unit tests) | outcomes mapped / inspect envelope | success and failure shapes hold without HTTP |
| TC-03 | FND-1 | Api.IntegrationTests | ✅ `Envelope_Does_Not_Leak_The_MessageType` | inspect serialised body | internal message-type representation absent |
| TC-04 | FND-5 | Api.IntegrationTests | `planned` — first 204-conventional operation is customer delete (US-117) | delete ticket-free customer / inspect status | 200 with envelope, not 204 |

## Notes

The no-204 rule is not pedantry — a 204 has no body, hence no code/message, forcing a client special-case exactly where nothing went wrong.

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
