# US-126 · An empty list never looks like a failure

| Field | Value |
|---|---|
| **Story** | `US-126` *(was `US-1.38`)* |
| **Epic** | [EPIC-04 Agent dashboard](../epics/EPIC-04-agent-dashboard.md) |
| **Feature** | [`FEAT-05` Ticket queue](../delivery-plan.md#feat-05--ticket-queue) |
| **Layer** | Frontend |
| **Ships with** | [US-013](./US-013-filter-the-queue.md) *(backend)*, [US-035](./US-035-agent-sees-own-work.md) *(backend)* |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [2 — Customers, ticket capture and queue](../delivery-plan.md#sprint-2--customers-ticket-capture-and-queue) · Slice S1 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-4.3 |
| **Spec criteria** | AC-58 |
| **Depends on** | [US-038](./US-038-usable-ticket-list.md) |

## Story

**As an agent**, **I want** to be able to tell "nothing here" from "something broke", **so that** I do not assume a quiet queue when the server is down.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- Loading, empty and error states stay visually distinct on every data view; an empty result never
  reads as a failure and a failure never renders as blank (from AC-58).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Distinct loading, empty, error states (spec AC-58)

Loading, empty and error states are visually distinct on every data view. An empty result never
looks like a failure, and a failure never looks empty.

## SQL tables

None — frontend story.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-58 | Frontend (Vitest) | PASS `AC58: shows a loading state while the request is in flight` | request in flight / inspect rendered DOM | the distinct loading state |
| TC-02 | AC-58 | Frontend (Vitest) | PASS `AC58: a successful empty result renders the empty state, with no retry offered` | flush an empty page / inspect | distinct empty state — not the error state, not blank |
| TC-03 | AC-58 | Frontend (Vitest) | PASS `AC58: a failed request renders the error state, never the empty state` | flush a 500 / inspect | distinct error state; **never** an empty list |
| TC-04 | AC-58 | Frontend (Vitest) | PARTIAL — now proven on **two** views: the queue (`AC58: a failed request renders the error state, never the empty state`) and ticket detail (`AC58: a failed load renders the error state, not an empty ticket`, added 2026-08-26). Customer screens are cut, so a third does not exist. The shared `AsyncState` union makes the guarantee structural rather than per-screen | repeat TC-01..03 for each data view (list, detail, customers) / compare states | all three views distinguish all three states |

## Notes

This story exists because catching an error into an empty array is the default mistake in this codebase's idiom, and it renders a server outage as "no tickets" — the most dangerous possible misreport for a support queue.

## Open questions

None.

## Status evidence

Implemented over the existing `AsyncState` closed union and the `CsLoadingState` / `CsEmptyState` /
`CsErrorState` components, first demonstrated on the ticket queue.

AC-58 -> `AC58: shows a loading state while the request is in flight`, `AC58: a failed request
renders the error state, never the empty state`, `AC58: a successful empty result renders the empty
state, with no retry offered`, `AC58: retrying re-issues the request`.

The retry button's presence in the error state and its absence in the empty state is the
load-bearing assertion: it is both the honest signal and the visual difference. `empty()` is
reachable only from the success callback, so `catchError(() => of([]))` cannot render an outage as
"no tickets".

Run 2026-08-26: `npx ng test admin-app --watch=false` - 39 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
