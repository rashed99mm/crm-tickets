# US-108 · Domain base types with identity and component equality

| Field | Value |
|---|---|
| **Story** | `US-108` *(was `US-1.08`)* |
| **Epic** | [EPIC-12 Platform features](../epics/EPIC-12-platform.md) |
| **Feature** | [`FEAT-01` Platform foundation](../delivery-plan.md#feat-01--platform-foundation) |
| **Layer** | Backend |
| **Ships with** | — Enabler. No user-facing surface, so nothing pairs with it. |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Internal — Maintainer |
| **Priority** | P1 |
| **Sprint** | [1 — Foundation and authentication](../delivery-plan.md#sprint-1--foundation-and-authentication) · Slice S1 |
| **Estimate** | 3 points |
| **Status** | `superseded` |
| **BRD requirements** | NFR-19 |
| **Spec criteria** | FND-22, FND-27, FND-28 |
| **Depends on** | — |

## Story

**As a maintainer**, **I want** entities and value objects with correct equality semantics, **so that** identity bugs are impossible rather than merely unlikely.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- Entities carry an id with a protected setter and identity-based equality through `BaseEntity<TId>`
  (from FND-22).
- Value objects compare by component (from FND-27).
- Aggregate roots are marked, and child entities are not (from FND-28).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Identity-based entity equality (spec FND-22)

Given `BaseEntity<TId>`, when inspected, then it provides an id with a protected setter and
identity-based equality.

#### AC2 — Value objects compare by component (spec FND-27)

Given value objects, when compared, then comparison is by component.

#### AC3 — Roots marked, children unmarked (spec FND-28)

Given aggregate roots and child entities, when marked up, then roots are marked and child entities
are not.

## SQL tables

None — this story defines in-memory types. The column-level consequences of `IAuditable` and
`ISoftDeletable` are tabulated in the [S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md)
and exercised by US-109.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | FND-22 | Domain.Tests | ✅ `BaseEntityTests.Entities_With_Same_Type_And_Id_Are_Equal` | two instances, same type + id / compare / — | equal |
| TC-02 | FND-22 | Domain.Tests | ✅ `Entities_With_Same_Type_And_Different_Id_Are_Not_Equal` + `Entities_Of_Different_Types_With_Same_Id_Are_Not_Equal` | differing id or type / compare / — | not equal |
| TC-03 | FND-22 | Domain.Tests | ✅ `Entities_With_Default_Id_Are_Never_Equal` | two unsaved entities (default ids) / compare / — | never equal |
| TC-04 | FND-22 | Domain.Tests | ✅ `Equal_Entities_Share_A_HashCode` | equal entities / hash / — | identical hash codes |
| TC-05 | FND-27 | Domain.Tests | ✅ `ValueObjectTests` (4 tests) + `EmailTests.Emails_Differing_Only_By_Case_Are_Equal` | value objects with equal/different components / compare / — | component equality; normalised email equality |
| TC-06 | FND-28 | Domain.Tests | `planned` — entities do not exist yet | `Customer`/`Ticket` declared, children declared / reflect on markers / — | roots marked `IAggregateRoot`; notes, attachments, history are not |

## Notes

Two entities with default ids must not compare equal. The naive implementation makes every unsaved entity equal to every other unsaved entity, which corrupts any set or dictionary they are put into before anything reaches a database.

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
