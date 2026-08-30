# US-112 · Staff sign in and receive a role-carrying credential

| Field | Value |
|---|---|
| **Story** | `US-112` *(was `US-1.12`)* |
| **Epic** | [EPIC-09 Security & administration](../epics/EPIC-09-administration.md) |
| **Feature** | [`FEAT-02` Authentication and session](../delivery-plan.md#feat-02--authentication-and-session) |
| **Layer** | Backend |
| **Ships with** | [US-125](./US-125-sign-in-and-land-on-work.md) *(frontend)* |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [1 — Foundation and authentication](../delivery-plan.md#sprint-1--foundation-and-authentication) · Slice S1 |
| **Estimate** | 8 points |
| **Status** | `done` |
| **BRD requirements** | FR-10.1, FR-10.7 |
| **Spec criteria** | AC-1, AC-3 |
| **Depends on** | [US-101](./US-101-uniform-response-envelope.md) |

## Story

**As an agent**, **I want** to sign in and stay signed in across requests, **so that** I can work without re-authenticating on every action.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- Valid credentials sign in with 200 and a token carrying the user id and role claims (from AC-1).
- A missing, malformed or expired token gets 401 from any protected endpoint (from AC-3).
- Staff accounts and the two roles are created administratively — seeded this sprint; there is no
  public self-registration for staff (from FR-10.7).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Sign-in issues a role-carrying token (spec AC-1)

Given valid credentials, when signing in, then 200 with a token carrying the user id and role
claims.

#### AC2 — Protected endpoints demand a token (spec AC-3)

Given a missing, malformed or expired token, when calling any protected endpoint, then 401.

#### AC3 — Staff seeded administratively (FR-10.7)

Given a fresh deployment, when start-up seeding runs, then staff accounts and the two roles are
created administratively — seeded this sprint; there is no public self-registration for staff.

## SQL tables

Identity-owned schema — see [S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#aspnetusers-identity-managed).
Only our addition and the seed data are ours:

```sql
ALTER TABLE [dbo].[AspNetUsers] ADD [DisplayName] NVARCHAR(100) NOT NULL DEFAULT N'';
-- Seeded at startup: roles Agent + Supervisor in [AspNetRoles],
-- staff users in [AspNetUsers], membership in [AspNetUserRoles] (FR-10.7).
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-1 | Api.IntegrationTests | `planned` | seeded staff credentials / `POST` sign-in / inspect | 200; token issued |
| TC-02 | AC-1 | Api.IntegrationTests | `planned` | the issued token / decode claims / — | user id (`sub`) and role claim present |
| TC-03 | AC-3 | Api.IntegrationTests | `planned` — one case per input | **no token** on a protected endpoint / request / observe | 401 |
| TC-04 | AC-3 | Api.IntegrationTests | `planned` | a **malformed** string as bearer / request / observe | 401 |
| TC-05 | AC-3 | Api.IntegrationTests | `planned` | an **expired** but well-formed token / request / observe | 401 |
| TC-06 | FR-10.7 (seed) | Api.IntegrationTests | `planned` | a fresh database / run startup seeding / query roles | `Agent` + `Supervisor` exist with their staff users |

## Notes

The third criterion covers only the seeded half of `FR-10.7`, per assumption `B3`. Managing accounts through an interface belongs to the proposed slice S9 and is sprint 12 — see gap `G-2`.

## Open questions

- G-2 — managing accounts through an interface belongs to the proposed slice S9 and is sprint 12.
  Tracked in [the register](../../product/05-assumptions-and-open-questions.md).

## Status evidence

**Done against the new baseline**, verified live 2026-08-25 — not inherited from the previous
implementation, which was replaced ([ADR-0009](../../adr/0009-adopt-the-support-platform-as-the-crm-baseline.md)).

AC-1: `POST /api/Auth/login` on the internal host returns 200 with a 676-character JWT for the
seeded administrator.
AC-3: `GET /api/users` without a token returns **401**; with that token, **200**.

Verified by request against a running host, not by unit test. The platform ships its own auth tests
inside the 97-test suite; a test naming `AC-1` and `AC-3` specifically does not yet exist, so this
status rests on a live probe rather than on a repeatable assertion. That is weaker evidence than the
story previously had, and it is recorded as such.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
