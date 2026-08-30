# US-114 · Role permissions refuse what the role may not do

| Field | Value |
|---|---|
| **Story** | `US-114` *(was `US-1.14`)* |
| **Epic** | [EPIC-09 Security & administration](../epics/EPIC-09-administration.md) |
| **Feature** | [`FEAT-02` Authentication and session](../delivery-plan.md#feat-02--authentication-and-session) |
| **Layer** | Backend |
| **Ships with** | [US-125](./US-125-sign-in-and-land-on-work.md) *(frontend)* |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Team Lead |
| **Priority** | P0 |
| **Sprint** | [1 — Foundation and authentication](../delivery-plan.md#sprint-1--foundation-and-authentication) · Slice S1 |
| **Estimate** | 3 points |
| **Status** | `superseded` |
| **BRD requirements** | FR-10.5 |
| **Spec criteria** | AC-4 |
| **Depends on** | [US-112](./US-112-staff-sign-in.md) |

## Story

**As a supervisor**, **I want** supervisor-only operations closed to agents, **so that** the role boundary is real rather than advisory.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- An agent's token calling a supervisor-only endpoint is refused with 403 (from AC-4).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Agents refused supervisor-only operations (spec AC-4)

Given an agent's token, when calling a supervisor-only endpoint, then 403.

## SQL tables

None directly — enforcement is endpoint policy. The data it reads is role membership in
`[AspNetUserRoles]`, seeded per US-112 (see
[S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#aspnetusers-identity-managed)).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-4 | Api.IntegrationTests | `planned` | an `Agent` token / call a supervisor-only endpoint / observe | 403 |
| TC-02 | AC-4 (contrast) | Api.IntegrationTests | `planned` | a `Supervisor` token on the same endpoint / request / observe | succeeds — proves 403 came from the role, not the route |

## Notes

This is the endpoint-level half of authorization. It is necessary and not sufficient: the per-record half is US-120, and neither substitutes for the other.

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
