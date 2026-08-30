# US-914 · Ticket Create: Type-Ahead Picker

| Field | Value |
|---|---|
| **Story** | `US-914` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-30`](../delivery-plan.md#feat-30) |
| **Layer** | Frontend |
| **Ships with** | [US-903](./US-903-assignment-required-and-self-assign.md) *(Backend)* |
| **Actor** | Agent |
| **Priority** | P0 |
| **Sprint** | 19 — UX redesign |
| **Estimate** | 4 points |
| **Status** | `not started` |

## Story

**As an agent**, **I want** to type to find a customer or category, **so that** I am not scrolling a
plain 20-row `<select>` to find "Ahmed".

## Business rules

- Customer and category pickers become type-ahead (filter over the API-loaded options — debounced,
  keyboard navigable); a chosen option shows as a chip/clearable selection.
- Server field errors still land on the control (existing `fieldError(field)` contract).

## Acceptance criteria

#### AC1 — Type-ahead pickers

Given the create form, then customer and category are searchable type-ahead pickers over API-loaded
sources, and server field errors still land on the control.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Component | `TicketCreate_CustomerTypeahead_Filters` | typing filters options |
| TC-02 | AC1 | Component | `TicketCreate_CategoryTypeahead_Selects` | selection becomes chip |
| TC-03 | AC1 | Component | `TicketCreate_ServerError_OnControl` | field error shown on control |

## SQL tables

None.

## Notes

Replaces the plain `<select>` customer/category pickers with a shared type-ahead over the existing
customer and category lookups.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.