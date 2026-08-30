# US-125 · Sign in and land on the work

| Field | Value |
|---|---|
| **Story** | `US-125` *(was `US-1.36`)* |
| **Epic** | [EPIC-04 Agent dashboard](../epics/EPIC-04-agent-dashboard.md) |
| **Feature** | [`FEAT-02` Authentication and session](../delivery-plan.md#feat-02--authentication-and-session) |
| **Layer** | Frontend |
| **Ships with** | [US-112](./US-112-staff-sign-in.md) *(backend)*, [US-113](./US-113-failed-sign-in-reveals-nothing.md) *(backend)*, [US-114](./US-114-role-permissions-refuse.md) *(backend)*, [US-115](./US-115-credentials-never-emitted.md) *(backend)* |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [1 — Foundation and authentication](../delivery-plan.md#sprint-1--foundation-and-authentication) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `superseded` |
| **BRD requirements** | FR-4.1 |
| **Spec criteria** | AC-55, AC-56 |
| **Depends on** | [US-112](./US-112-staff-sign-in.md) *(sprint 1)* |

## Story

**As an agent**, **I want** to sign in and arrive at my tickets, **so that** I can start working immediately.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- Valid credentials land on the ticket list; invalid ones show a visible error without any
  navigation (from AC-55).
- Opening a protected route with no session redirects to sign-in (from AC-56).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Valid sign-in reaches tickets (spec AC-55)

Given valid credentials on the sign-in form, then the user reaches the ticket list; given invalid,
a visible error appears and no navigation occurs.

#### AC2 — No session redirects to sign-in (spec AC-56)

Given no session, when opening a protected route directly, then redirect to sign-in.

## SQL tables

None — frontend story. It consumes the sign-in endpoint backed by `AspNetUsers`
([S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#aspnetusers-identity-managed)).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-55 | Frontend (Vitest) | `planned` | valid credentials submitted / auth service called / inspect | navigates to the ticket list |
| TC-02 | AC-55 | Frontend (Vitest) | `planned` — the no-navigation half | invalid credentials / submit / inspect router state | visible error; **no navigation occurred** |
| TC-03 | AC-56 | Frontend (Vitest) | `planned` | no session in store / open a protected route directly / observe | redirect to sign-in |
| TC-04 | AC-55/56 | E2E (Playwright) | `planned` — folded into the AC-64 journey | real browser flow through both paths / observe | same outcomes end to end |

## Notes

"No navigation occurs" is worth asserting: a form that navigates and then bounces back shows the user a flash of the protected page, which reads as a bug even when nothing was exposed.

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
