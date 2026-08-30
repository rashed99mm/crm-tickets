# US-110 · The dependency rule is enforced by the build

| Field | Value |
|---|---|
| **Story** | `US-110` *(was `US-1.10`)* |
| **Epic** | [EPIC-12 Platform features](../epics/EPIC-12-platform.md) |
| **Feature** | [`FEAT-01` Platform foundation](../delivery-plan.md#feat-01--platform-foundation) |
| **Layer** | Backend |
| **Ships with** | — Enabler. No user-facing surface, so nothing pairs with it. |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Internal — Assessor |
| **Priority** | P0 |
| **Sprint** | [1 — Foundation and authentication](../delivery-plan.md#sprint-1--foundation-and-authentication) · Slice S1 |
| **Estimate** | 2 points |
| **Status** | `superseded` |
| **BRD requirements** | NFR-19, NFR-20 |
| **Spec criteria** | FND-29 |
| **Depends on** | — |

## Story

**As an assessor**, **I want** the architectural dependency rule checked mechanically, **so that** the architecture claim rests on evidence rather than on discipline.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- `Domain` holds zero project references and no persistence, Identity or web packages, asserted by a
  test that reads the project file rather than by review (from FND-29).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Domain stays dependency-free (spec FND-29)

Given the `Domain` project, when it is checked by a test that reads the project file, then it has
zero project references and no persistence, Identity or web packages — not asserted by review.

## SQL tables

None — the criterion is precisely that `Domain` has none: zero project references and no
persistence package (FND-29).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | FND-29 | Domain.Tests | ✅ `ArchitectureTests.Domain_Has_No_Dependency_On_Other_Project_Or_Infrastructure_Package` | loaded assemblies / inspect references / — | no reference to Application, Infrastructure or Api |
| TC-02 | FND-29 | Domain.Tests | ✅ `Domain_Csproj_Declares_No_Project_Or_Package_References` | `Domain.csproj` / parse as XML / count elements | zero `ProjectReference`, zero `PackageReference` |

## Notes

This is the one architectural claim an assessor can check mechanically, so it is the one that must not rest on review. The test reads the project file directly as well as inspecting loaded assemblies, because an assembly-only check passes while an unused-but-present package reference sits in the csproj waiting for someone to use it.

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
