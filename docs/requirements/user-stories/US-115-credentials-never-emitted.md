# US-115 · Credentials never appear anywhere

| Field | Value |
|---|---|
| **Story** | `US-115` *(was `US-1.15`)* |
| **Epic** | [EPIC-09 Security & administration](../epics/EPIC-09-administration.md) |
| **Feature** | [`FEAT-02` Authentication and session](../delivery-plan.md#feat-02--authentication-and-session) |
| **Layer** | Backend |
| **Ships with** | [US-125](./US-125-sign-in-and-land-on-work.md) *(frontend)* |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Internal — Data protection owner |
| **Priority** | P0 |
| **Sprint** | [1 — Foundation and authentication](../delivery-plan.md#sprint-1--foundation-and-authentication) · Slice S1 |
| **Estimate** | 2 points |
| **Status** | `superseded` |
| **BRD requirements** | FR-10.4, NFR-5 |
| **Spec criteria** | AC-5 |
| **Depends on** | [US-112](./US-112-staff-sign-in.md) |

## Story

**As a data protection owner**, **I want** certainty that no password or hash is ever emitted, **so that** a log export or an error response cannot become a credential leak.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- No endpoint, log line or error response ever contains a password or password hash (from AC-5).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Credentials never emitted anywhere (spec AC-5)

Given any endpoint, log line or error response, when inspected, then none ever contains a password
or password hash.

## SQL tables

The risk lives in a column we do not own: `[AspNetUsers].[PasswordHash]` (Identity-managed, see the
[S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#aspnetusers-identity-managed)).
No DTO may ever project it.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5 | Api.IntegrationTests | `planned` | sign-in succeeds and fails once each / serialise both responses / search JSON | neither `password` nor any hash fragment appears |
| TC-02 | AC-5 | Api.IntegrationTests | `planned` | the same request cycle with an in-memory log sink / search every emitted line | no password or hash in any log record |

## Notes

Worth its own story despite its size, because it is a property of everything rather than of one endpoint, and the usual way it breaks is a DTO that serialises an entity wholesale.

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
