# US-038 · The ticket list is usable

| Field | Value |
|---|---|
| **Story** | `US-038` *(was `US-1.37`)* — rule proposal: *Filter and Sort Ticket Workload* |
| **Epic** | [EPIC-04 Agent dashboard](../epics/EPIC-04-agent-dashboard.md) |
| **Feature** | [`FEAT-05` Ticket queue](../delivery-plan.md#feat-05--ticket-queue) |
| **Layer** | Frontend |
| **Ships with** | [US-013](./US-013-filter-the-queue.md) *(backend)*, [US-035](./US-035-agent-sees-own-work.md) *(backend)* |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [2 — Customers, ticket capture and queue](../delivery-plan.md#sprint-2--customers-ticket-capture-and-queue) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-4.1, FR-12.6 |
| **Spec criteria** | AC-57 |
| **Depends on** | [US-013](./US-013-filter-the-queue.md) *(sprint 2)*, [US-125](./US-125-sign-in-and-land-on-work.md) |

## Story

**As an agent**, **I want** a paged list with a status filter and a "my tickets" toggle, **so that** I can find what I need to work on next.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- The ticket list pages results and offers a working status filter and a "my tickets" toggle
  (from AC-57).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Paged list, filter, toggle (spec AC-57)

Given the ticket list, then paged results with a working status filter and a "my tickets" toggle.

## SQL tables

None — frontend story. Reads `Tickets` through the list endpoint
([S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#tickets)).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-57 | Frontend (Vitest + `HttpTestingController`) | PASS `AC57: renders the tickets returned by the api` | list component loads / verify request / flush a page of tickets | request carries paging params; items rendered |
| TC-02 | AC-57 | Frontend (Vitest) | PASS `AC57: the status filter refetches with the selected status` | a status chosen / filter applied / inspect outgoing query + rendered rows | filtered request issued; only matching rows shown |
| TC-03 | AC-57 | Frontend (Vitest) | PASS `AC57: the my-tickets toggle requests only the caller's own work` — also asserts no `assigneeId` is sent | toggle on / inspect outgoing query | `mine=true` sent, derived from session not an input field |
| TC-04 | AC-57 | Frontend (Vitest) | PASS `AC57: advances the page parameter when the next page is requested` | next page clicked / inspect outgoing request | page parameter advances |

## Notes

This is the screen an agent looks at all day, so it is the one place where a slow query is felt continuously rather than occasionally. `NFR-1` sets p95 under 500 ms at 100,000 tickets.

## Open questions

None.

## Status evidence

Implemented as `admin-app/features/tickets/ticket-queue.component.ts`, replacing the
`ticket-queue.placeholder.ts` stub, which is deleted.

AC-57 -> `AC57: renders the tickets returned by the api`, `AC57: the status filter refetches with
the selected status`, `AC57: the my-tickets toggle requests only the caller's own work`.

**Known limitation:** paging is previous/next, with a page size duplicated between the request and
the `hasMore` calculation rather than read from the response.

Run 2026-08-26: `npx ng test admin-app --watch=false` - 39 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
