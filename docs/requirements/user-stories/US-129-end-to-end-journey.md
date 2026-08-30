# US-129 · One journey proves the whole flow persists

| Field | Value |
|---|---|
| **Story** | `US-129` *(was `US-1.42`)* |
| **Epic** | [EPIC-04 Agent dashboard](../epics/EPIC-04-agent-dashboard.md) |
| **Feature** | [`FEAT-11` End-to-end journey](../delivery-plan.md#feat-11--end-to-end-journey) |
| **Layer** | Frontend |
| **Ships with** | — Terminal by design. It exercises FEAT-02 through FEAT-08 in one browser flow. |
| **Actor** | Internal — Reviewer |
| **Priority** | P1 |
| **Sprint** | [4 — Contract hardening, localisation and the journey](../delivery-plan.md#sprint-4--contract-hardening-localisation-and-the-journey) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | NFR-19 |
| **Spec criteria** | AC-64 |
| **Depends on** | [US-128](./US-128-ticket-detail-with-guarded-actions.md) |

## Story

**As a reviewer**, **I want** a single end-to-end test covering the real path, **so that** the integration between frontend, API and database is demonstrated rather than assumed.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- One browser journey — sign in, create, assign, change status, reload — confirms the status change
  and its history persisted (from AC-64).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Single journey proves persistence (spec AC-64)

One browser journey: sign in, create a ticket, assign it, change its status, reload, and confirm
the change and its history persisted.

## SQL tables

None directly — this story *proves* the persistence of others. The rows it must survive a reload
with are `Tickets` and `TicketHistory`
([S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md)).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-64 | E2E (Playwright) | `planned` — the single journey | sign in → create ticket → assign → change status → **reload** / observe each step | after reload the status change and its history are still shown |

## Notes

The reload is the point. Everything before it can pass against state held in a component; only the reload proves it reached the database and came back.

## Open questions

None.

## Status evidence

No implementation exists.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
