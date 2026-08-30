# US-127 · Create a ticket through a form that agrees with the server

| Field | Value |
|---|---|
| **Story** | `US-127` *(was `US-1.39`)* |
| **Epic** | [EPIC-04 Agent dashboard](../epics/EPIC-04-agent-dashboard.md) |
| **Feature** | [`FEAT-04` Ticket capture](../delivery-plan.md#feat-04--ticket-capture) |
| **Layer** | Frontend |
| **Ships with** | [US-009](./US-009-raise-a-ticket.md) *(backend)* |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [2 — Customers, ticket capture and queue](../delivery-plan.md#sprint-2--customers-ticket-capture-and-queue) · Slice S1 |
| **Estimate** | 8 points |
| **Status** | `done` |
| **BRD requirements** | FR-2.1, FR-1.2 |
| **Spec criteria** | AC-59, AC-60 |
| **Depends on** | [US-009](./US-009-raise-a-ticket.md) *(sprint 2)*, [US-104](./US-104-field-keyed-validation-errors.md) *(sprint 1)* |

## Story

**As an agent**, **I want** the form to catch my mistakes and to show the server's objections on the right field, **so that** I can fix them without guessing.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- Client validation mirrors the server's rules; errors appear only after a field is touched, and
  submit is disabled while invalid and while in flight (from AC-59).
- Each server `errors[]` entry maps onto the form control named by its `field`, not into a generic
  banner; the top-level message may additionally be shown as a summary (from AC-60).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Client validation mirrors server (spec AC-59)

Client validation mirrors the server's rules, errors appear only after a field is touched, and
submit is disabled while invalid **and** while in flight.

#### AC2 — Field-keyed server errors (spec AC-60)

Given the server returns `errors[]`, each entry maps onto the form control named by its `field`,
not into a generic banner. The top-level message may additionally be shown as a summary.

## SQL tables

None — frontend story. Writes `Tickets` through the create endpoint
([S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#tickets)).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-59 | Frontend (Vitest) | PARTIAL `AC59: rejects a subject over 200 characters before submitting`. Priority is a fixed `<select>` of the four valid values, so an invalid one is unreachable from the UI and has no client test | subject cleared, over-length value, bad priority / inspect validators | same field errors the server would raise (AC-30 set) |
| TC-02 | AC-59 | Frontend (Vitest) | PASS `CsInputField` owns this rule and is covered by `common/ui/input-field.component.spec.ts`; this form composes it rather than re-implementing it | error present but field untouched / inspect DOM | no error shown; shows after blur/touch |
| TC-03 | AC-59 | Frontend (Vitest) | PASS `AC59: the submit button is disabled while the form is invalid` | invalid form / inspect submit button | disabled |
| TC-04 | AC-59 | Frontend (Vitest) | PASS `AC59: does not submit twice while a request is in flight` | valid form submitted, response pending / double-click submit | one HTTP call; button disabled until settle |
| TC-05 | AC-60 | Frontend (Vitest + `HttpTestingController`) | PASS `AC60: a server field error appears under the control it names, not in a banner` — asserts position via `aria-describedby` | flush a 400 whose `errors[]` names `subject` and `priority` / inspect DOM | messages bound to those controls; not only a banner |

## Notes

"And while in flight" is the half that gets missed, and it is how one impatient double-click becomes two tickets.

The second criterion is the payoff for the camelCase rule in US-104: mapping is a lookup by control name, with no translation table to drift.

## Open questions

None.

## Status evidence

Implemented as `admin-app/features/tickets/ticket-create.component.ts`, composing `CsInputField`
and the envelope interceptor rather than adding a second error-display path.

AC-59 -> `AC59: does not submit while the form is invalid`, `AC59: rejects a subject over 200
characters before submitting`, `AC59: does not submit twice while a request is in flight`.
AC-60 -> `AC60: a server field error appears under the control it names, not in a banner`, which
asserts the message's **position** through `aria-describedby`, and `AC60: a failure naming no field
renders at form level`.

**Known limitations:** the customer picker is a plain select over the first 20 customers, not a
typeahead; a picker whose own load fails renders empty rather than showing an error. Both are
recorded in `docs/superpowers/plans/EPIC-02-US-016-feat-04-ticket-capture-frontend/README.md`.

Run 2026-08-26: `npx ng test admin-app --watch=false` - 39 passed, 0 failed;
`npx ng test common --watch=false` - 55 passed, 0 failed; `npx ng build admin-app` clean.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
