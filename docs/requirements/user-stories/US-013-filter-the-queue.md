# US-013 · Work through the queue with filters

| Field | Value |
|---|---|
| **Story** | `US-013` *(was `US-1.22`)* — rule proposal: *Filter Tickets*; secondarily realizes *Search Tickets* (US-012) |
| **Epic** | [EPIC-02 Ticket management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-05` Ticket queue](../delivery-plan.md#feat-05--ticket-queue) |
| **Layer** | Backend |
| **Ships with** | [US-038](./US-038-usable-ticket-list.md) *(frontend)*, [US-126](./US-126-empty-never-looks-like-failure.md) *(frontend)* |
| **Actor** | Team Lead |
| **Priority** | P0 |
| **Sprint** | [2 — Customers, ticket capture and queue](../delivery-plan.md#sprint-2--customers-ticket-capture-and-queue) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-2.4, FR-2.5 |
| **Spec criteria** | AC-32, AC-33 |
| **Depends on** | [US-009](./US-009-raise-a-ticket.md) |

## Story

**As a supervisor**, **I want** to filter the ticket list by status, priority, assignee and customer, **so that** I can see the slice of the queue I am dealing with.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- Listing paginates newest first (from AC-32).
- Filters for status, priority, assignee and customer each narrow the list, and combine
  (from AC-33).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Paginated newest first (spec AC-32)

Given tickets exist, when listing, then a paginated envelope, newest first.

#### AC2 — Filters combine (spec AC-33)

Given filters for status, priority, assignee or customer, then only matching tickets, and filters
combine.

## SQL tables

`Tickets` read path — from the [S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#tickets):

```sql
CREATE INDEX IX_Tickets_Status_Priority ON [dbo].[Tickets] ([Status], [Priority]);
CREATE INDEX IX_Tickets_CustomerId      ON [dbo].[Tickets] ([CustomerId]);
-- newest first: ordered by [CreatedAtUtc] DESC / Id
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-32 | Api.IntegrationTests | PASS `AC32_GetTickets_ReturnsPagedNewestFirst` | tickets created in a known order / list page 1 / inspect items + `totalCount` | paginated envelope, newest first |
| TC-02 | AC-33 | Api.IntegrationTests | PASS `AC33_GetTickets_EachFilter_ReturnsOnlyMatching` (priority), `AC33_GetTickets_AssigneeFilter_ReturnsOnlyThatAgentsTickets` (assignee, closed 2026-08-26); `customerId` is exercised by every other test in the class | one filter at a time (status, priority, assignee, customer) / list ×4 / inspect | each returns only matches |
| TC-03 | AC-33 | Api.IntegrationTests | PASS `AC33_GetTickets_CombinedFilters_NarrowToIntersection` | two or more filters together / list / inspect | AND semantics, not if/else-first-filter |
| TC-04 | AC-33 (negative) | Api.IntegrationTests | PASS `AC34_GetTickets_MineWithNoTickets_Returns200EmptyPage` — an empty page in an intact envelope, never a 404 | filters matching nothing / list / inspect | empty `items`, envelope intact |

## Notes

Filters combining is the criterion that catches the common implementation error of an if/else chain that honours only the first filter supplied.

## Open questions

None.

## Status evidence

Implemented in Day 1 in `GetTicketsQuery`; **completed 2026-08-26** once assignment existed.

AC-32 -> `AC32_GetTickets_ReturnsPagedNewestFirst`. AC-33 ->
`AC33_GetTickets_EachFilter_ReturnsOnlyMatching`,
`AC33_GetTickets_CombinedFilters_NarrowToIntersection` (the one that proves filters compose rather
than overwrite), `AC33_GetTickets_UnknownStatusValue_Returns400`, and
`AC33_GetTickets_AssigneeFilter_ReturnsOnlyThatAgentsTickets` - the fourth filter, which could not
be tested in isolation until a ticket could be assigned.

Run 2026-08-26: 233 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
