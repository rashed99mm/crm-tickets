# US-104 · Validation failures arrive keyed to their field

| Field | Value |
|---|---|
| **Story** | `US-104` *(was `US-1.04`)* |
| **Epic** | [EPIC-12 Platform features](../epics/EPIC-12-platform.md) |
| **Feature** | [`FEAT-01` Platform foundation](../delivery-plan.md#feat-01--platform-foundation) |
| **Layer** | Backend |
| **Ships with** | — Enabler. No user-facing surface, so nothing pairs with it. |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Internal — Frontend developer |
| **Priority** | P0 |
| **Sprint** | [1 — Foundation and authentication](../delivery-plan.md#sprint-1--foundation-and-authentication) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `superseded` |
| **BRD requirements** | FR-1.2, FR-2.3 |
| **Spec criteria** | FND-9, FND-10, FND-11 |
| **Depends on** | — |

## Story

**As a frontend developer**, **I want** each validation error attached to the field that caused it, **so that** I can show it on the right control instead of in a banner.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- A validation failure returns 400 with top-level `VAL001` and one `errors[]` entry per failed field
  (from FND-9).
- Each error entry's `field` is camelCase and matches the request DTO property name (from FND-10).
- Failures across multiple fields all appear together in one response (from FND-11).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Errors keyed to their field (spec FND-9)

Given a validation failure, when the response is produced, then it is 400 with top-level `VAL001`
and one `errors[]` entry per failed field.

#### AC2 — Field names match the DTO (spec FND-10)

Given a failed field, when its error entry is read, then `field` is camelCase and matches the
request DTO property name.

#### AC3 — All failures in one response (spec FND-11)

Given multiple failures across multiple fields, when validation completes, then all of them appear
in one response.

## SQL tables

None — this story touches no persisted data.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | FND-9 | Application.Tests | ✅ `ResponseTests.ValidationFailure_Carries_Field_Errors` | a validation failure with two field errors / build / inspect | 400-shaped envelope, top-level `VAL001`, two `errors[]` entries |
| TC-02 | FND-9 | Application.Tests | ✅ `ResponseValidationBehaviorTests.Each_Field_Error_Carries_Its_Own_Val_Code` | multiple invalid fields / validate / inspect `errors[]` | one entry per field, each with its own `VAL0xx` |
| TC-03 | FND-10 | Application.Tests | ✅ `Field_Names_Are_CamelCase_To_Match_The_Request_Dto` | a DTO property in PascalCase / fail validation / inspect `field` | camelCase, matching the request DTO |
| TC-04 | FND-11 | Application.Tests | ✅ `Reports_Every_Failed_Field_In_One_Response` | several fields invalid at once / validate / count entries | every failed field present in the single response |

## Notes

The camelCase rule exists so the frontend can map an error onto a form control by name with no translation table. A server that returns `PascalCase` field names forces a mapping layer that will drift from the DTO it mirrors.

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
