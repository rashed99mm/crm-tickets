# US-912 · Ticket Queue Redesign (Search, Sort, Filter)

| Field | Value |
|---|---|
| **Story** | `US-912` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-30`](../delivery-plan.md#feat-30) |
| **Layer** | Frontend |
| **Ships with** | [US-903](./US-903-assignment-required-and-self-assign.md) *(Backend)* |
| **Actor** | Agent |
| **Priority** | P0 |
| **Sprint** | 19 — UX redesign |
| **Estimate** | 5 points |
| **Status** | `not started` |

## Story

**As an agent**, **I want** the queue to be searchable, sortable and filterable, **so that** I can
find my work instead of scrolling a paged table.

## Business rules

- Server-filtered search (subject/reference keyword), status/priority/assignee
  filters, escalation toggle preserved; sortable columns; rows navigate to detail.
- Existing `mine`/`unassigned` filters honoured (the dashboard's `/tickets?unassigned=true` link
  must actually land).
- Empty state is locality-distinct from error state.

## Acceptance criteria

#### AC1 — Search/sort/filter surface

Given the ticket queue, then a search box, sortable columns and status/priority/assignee filters
exist; the `unassigned` query param is read on load; rows link to detail.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Component | `TicketQueue_Search_FiltersServerSide` | refetch with keyword |
| TC-02 | AC1 | Component | `TicketQueue_UnassignedParam_AppliesFilter` | `unassigned=true` honored |
| TC-03 | AC1 | Component | `TicketQueue_SortableColumns` | click re-sorts |

## SQL tables

None.

## Notes

The ticket list query gains keyword and sort support; the queue screen gains the
search/sort/filter surface.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.