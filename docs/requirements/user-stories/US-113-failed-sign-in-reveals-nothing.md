# US-113 · A failed sign-in reveals nothing, lockout included

| Field | Value |
|---|---|
| **Story** | `US-113` *(was `US-1.13`)* |
| **Epic** | [EPIC-09 Security & administration](../epics/EPIC-09-administration.md) |
| **Feature** | [`FEAT-02` Authentication and session](../delivery-plan.md#feat-02--authentication-and-session) |
| **Layer** | Backend |
| **Ships with** | [US-125](./US-125-sign-in-and-land-on-work.md) *(frontend)* |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Internal — Security reviewer |
| **Priority** | P0 |
| **Sprint** | [1 — Foundation and authentication](../delivery-plan.md#sprint-1--foundation-and-authentication) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `superseded` |
| **BRD requirements** | FR-10.2, FR-10.3, BR-12 |
| **Spec criteria** | AC-2, AC-6, AC-67 |
| **Depends on** | [US-112](./US-112-staff-sign-in.md) |

## Story

**As a security reviewer**, **I want** failed authentication to disclose no information about the account, **so that** the sign-in form cannot be used to enumerate users.

## Business rules

- BR-12 — a wrong password and a locked account are indistinguishable: no distinct lockout code,
  message or status exists (BRD)

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Failure discloses nothing (spec AC-2)

Given invalid credentials, when signing in, then 401 with a message that does **not** reveal whether
the account exists.

#### AC2 — Lockout identical to wrong password (spec AC-6)

Given repeated failures beyond the configured threshold, when the threshold is exceeded, then the
account locks for the configured duration and further attempts return **401, identical to a wrong
password**.

#### AC3 — Same code and message (spec AC-67)

Given a locked-out attempt, when compared with invalid credentials, then the response carries the
**same code and message**.

## SQL tables

Identity-managed lockout columns only — see
[S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#aspnetusers-identity-managed):

```sql
-- Provided by Identity, relied on by AC-6:
--   [AccessFailedCount] INT NOT NULL,
--   [LockoutEnd]        DATETIMEOFFSET NULL
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-2 | Api.IntegrationTests | `planned` | a **known** user with a wrong password / sign-in / inspect body | 401; body reveals nothing about existence |
| TC-02 | AC-2 | Api.IntegrationTests | `planned` — the enumeration half | an **unknown** user / sign-in / compare body with TC-01 | byte-identical response to TC-01 |
| TC-03 | AC-6 | Api.IntegrationTests | `planned` | failures beyond the threshold / attempt again inside the window / observe | 401; account locked for the configured duration |
| TC-04 | AC-67 | Api.IntegrationTests | `planned` | locked-out attempt vs wrong-password attempt / compare full bodies | same code and same `ar`/`en` message |
| TC-05 | AC-67 | Application.Tests | ✅ `There_Is_No_Distinct_Lockout_Key` (the message-layer guard lives in US-107) | the domain-key enum / search for a lockout member / — | none exists |

## Notes

A distinct status code, code or message for lockout confirms the account exists, which defeats the purpose of the first criterion. The absence of a lockout code is already asserted by a test in US-107, so this story inherits a guard rather than relying on care.

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
