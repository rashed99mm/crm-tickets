# US-105 · The validation pipeline runs without reflection, and is proven to run

| Field | Value |
|---|---|
| **Story** | `US-105` *(was `US-1.05`)* |
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
| **BRD requirements** | FR-1.2 |
| **Spec criteria** | FND-12, FND-13, FND-13a |
| **Depends on** | [US-104](./US-104-field-keyed-validation-errors.md) |

## Story

**As a maintainer**, **I want** validation wired through a statically typed pipeline that is proven to execute, **so that** the behaviour cannot be silently bypassed or quietly slow.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- Message keys travel through the validation library's error-code mechanism, never as prose embedded
  in the validator (from FND-12).
- The validation pipeline behaviour uses no runtime reflection (from FND-13).
- A test proves the behaviour actually executes — a request failing validation produces the envelope
  rather than the framework's default (from FND-13a).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Message keys ride error codes (spec FND-12)

Given validators carrying message keys, when they report errors, then the key travels through the
validation library's error-code mechanism, not through prose embedded in the validator.

#### AC2 — Pipeline uses no reflection (spec FND-13)

Given the validation pipeline behaviour, when its implementation is inspected, then it uses no
runtime reflection.

#### AC3 — Behaviour proven to execute (spec FND-13a)

Given a request that fails validation, when it goes through the pipeline, then the behaviour
actually executes — producing the envelope rather than the framework's default.

## SQL tables

None — this story touches no persisted data.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | FND-13a | Application.Tests | ✅ `ResponseValidationBehaviorTests.Behavior_Actually_Runs_And_Short_Circuits_The_Handler` | a request failing validation / send through MediatR / observe | envelope with `VAL001`; handler never executed |
| TC-02 | FND-13a | Application.Tests | ✅ `Valid_Request_Reaches_The_Handler` | a valid request / send / observe | handler runs — the pipeline passes successes through |
| TC-03 | FND-13 | Api.IntegrationTests | ✅ `ApplicationWiringTests.AddApplication_Wires_The_Validation_Behavior` | composed API / resolve the pipeline / inspect | behavior registered and resolvable without reflection |
| TC-04 | FND-12 | Application.Tests | ✅ `Each_Field_Error_Carries_Its_Own_Val_Code` (US-104 TC-02) | validators using the error-code mechanism / validate / inspect codes | per-field codes carried by the mechanism, not prose |

## Notes

The third criterion is the one that matters, and it is why this is a separate story. A validation pipeline that is registered but never invoked passes every unit test of its own logic and validates nothing. The static-abstract factory on the response type is what removes the reflection.

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
