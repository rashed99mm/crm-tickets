# US-102 · One place decides the HTTP status

| Field | Value |
|---|---|
| **Story** | `US-102` *(was `US-1.02`)* |
| **Epic** | [EPIC-12 Platform features](../epics/EPIC-12-platform.md) |
| **Feature** | [`FEAT-01` Platform foundation](../delivery-plan.md#feat-01--platform-foundation) |
| **Layer** | Backend |
| **Ships with** | — Enabler. No user-facing surface, so nothing pairs with it. |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Internal — Maintainer |
| **Priority** | P0 |
| **Sprint** | [1 — Foundation and authentication](../delivery-plan.md#sprint-1--foundation-and-authentication) · Slice S1 |
| **Estimate** | 3 points |
| **Status** | `superseded` |
| **BRD requirements** | FR-11.3, NFR-6 |
| **Spec criteria** | FND-4, FND-8 |
| **Depends on** | — |

## Story

**As a maintainer**, **I want** the outcome-to-status mapping to exist exactly once, **so that** a new failure type cannot be given a different status by accident.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- The outcome-to-status mapping exists in exactly one place and no handler chooses a status of its
  own (from FND-4).
- No response body contains a stack trace, SQL text or a connection string — including for an
  unhandled exception (from FND-8).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Mapped in exactly one place (spec FND-4)

Given any outcome becoming a response, when its HTTP status is decided, then `MessageType` maps to
an HTTP status in exactly one place and no handler chooses a status.

#### AC2 — No internals leak into bodies (spec FND-8)

Given any response body, including one produced by an unhandled exception, when inspected, then it
contains no stack trace, SQL text or connection string.

## SQL tables

None — this story touches no persisted data.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | FND-4 | Api.IntegrationTests | ✅ `StatusMappingTests.Every_MessageType_Maps_To_Its_Documented_Status` | each of the ten `MessageType` members, parameterised / map / inspect | exactly its documented status |
| TC-02 | FND-4 | Api.IntegrationTests | ✅ `Every_Enum_Member_Is_Mapped` | the enum's members / reflect over the mapping table / — | no member without an entry |
| TC-03 | FND-8 | Api.IntegrationTests | ✅ `HealthEndpointTests.Unknown_Route_Returns_404_Without_An_Html_Error_Page` | unknown route / request / inspect body | envelope 404, no HTML error page |
| TC-04 | FND-8 | Api.IntegrationTests | `planned` — needs a forced unhandled exception | a handler that throws / request / inspect body | 500, code `ERR900`, generic message; no stack trace, SQL or connection string |

## Notes

Two mappings in two places diverge silently, and the divergence shows up as one endpoint returning 400 where its neighbour returns 409 for the same condition. A test asserting every enum member is mapped is what stops a new `MessageType` from defaulting to 500.

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
