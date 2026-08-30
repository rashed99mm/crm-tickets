# US-107 · The build fails when a code has no message

| Field | Value |
|---|---|
| **Story** | `US-107` *(was `US-1.07`)* |
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
| **BRD requirements** | FR-12.2, NFR-11 |
| **Spec criteria** | FND-18, FND-19, FND-21 |
| **Depends on** | [US-106](./US-106-bilingual-message-catalogue.md) |

## Story

**As a maintainer**, **I want** a test that fails the build when any code lacks a message in either language, **so that** a half-translated catalogue cannot reach a release.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- A guard test asserts every code constant has a catalogue entry non-empty in both languages
  (from FND-18).
- An unmapped domain key is caught by that guard at build time rather than degrading silently at
  runtime (from FND-19).
- Account lockout returns the same code and message as invalid credentials — no distinct lockout
  code exists (from FND-21).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Guard covers every code (spec FND-18)

Given every code constant, when the guard test runs, then each has a catalogue entry non-empty in
both languages.

#### AC2 — Unmapped keys caught at build time (spec FND-19)

Given an unmapped domain key, when it appears, then the guard catches it at build time rather than
degrading silently at runtime.

#### AC3 — Lockout indistinguishable from bad password (spec FND-21)

Given account lockout, when the response is produced, then it returns the same code and message as
invalid credentials — no distinct lockout code exists.

## SQL tables

None — this story touches no persisted data.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | FND-18 | Application.Tests | ✅ `SystemCodeCoverageTests.Every_Domain_Key_Has_A_Non_Empty_Message_In_Both_Languages` | every code constant / reflect over `Resources.yml` / — | non-empty `ar` **and** `en` for each |
| TC-02 | FND-18 | Application.Tests | ✅ `Resources_Yml_Has_No_Orphan_Keys` | every catalogue key / reflect over code constants / — | no key without a constant |
| TC-03 | FND-19 | Application.Tests | ✅ `Every_Domain_Key_Is_Mapped_To_A_Code` + `Unmapped_Key_Throws_Rather_Than_Degrading_Silently` + `Every_Mapped_Key_Is_Declared_On_DomainKey` | the map and the enum / cross-check both ways / misuse an unmapped key | complete mapping; unmapped use throws at the map, never degrades |
| TC-04 | FND-21 | Application.Tests | ✅ `There_Is_No_Distinct_Lockout_Key` | the domain-key enum / search for a lockout member / — | none exists |

## Notes

The third criterion is a security requirement wearing a localisation costume. A distinct lockout message confirms the account exists, which is exactly what US-113 refuses to disclose — so the absence of a lockout code is asserted by a test named for it, rather than left to whoever adds codes next.

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
