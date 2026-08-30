# US-004 · Find a customer in a long list

| Field | Value |
|---|---|
| **Story** | `US-004` *(was `US-1.18`)* — rule proposal: *Search Customers* |
| **Epic** | [EPIC-01 Customer management](../epics/EPIC-01-customer-management.md) |
| **Feature** | [`FEAT-03` Customer records](../delivery-plan.md#feat-03--customer-records) |
| **Layer** | Backend |
| **Ships with** | — API-only in S1. The spec defines no frontend criterion for customer management screens - see gap G-5. |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [2 — Customers, ticket capture and queue](../delivery-plan.md#sprint-2--customers-ticket-capture-and-queue) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-1.4, FR-1.5, NFR-2 |
| **Spec criteria** | AC-10, AC-11, AC-13 |
| **Depends on** | [US-001](./US-001-create-a-customer.md) |

## Story

**As an agent**, **I want** to page and search the customer list, **so that** I can find someone without scrolling through everyone.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- Listing pages through a paged envelope carrying `items`, `page`, `pageSize` and `totalCount`
  under `data` (from AC-10).
- A page size above the server maximum is refused with 400 rather than clamped (from AC-11).
- Search matches name or email, case-insensitively (from AC-13).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Paged listing envelope (spec AC-10)

Given customers exist, when listing, then a paged envelope carrying `items`, `page`, `pageSize`
and `totalCount` under `data`.

#### AC2 — Oversized page size refused (spec AC-11)

Given a page size above the server maximum, then 400.

#### AC3 — Case-insensitive search match (spec AC-13)

Given a search term, then only customers whose name or email matches, case-insensitively.

## SQL tables

`Customers` read path — from the [S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#customers):

```sql
CREATE INDEX IX_Customers_Name ON [dbo].[Customers] ([Name]);
-- search matches Name OR Email, case-insensitively (AC-13)
-- soft-deleted rows are excluded by the global query filter (FND-25)
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-10 | Api.IntegrationTests | PASS `AC10_GetCustomers_ReturnsPagedEnvelope` — field is `pageIndex`, not `page` (see D4) | several customers / `GET /customers?page=2&pageSize=5` / inspect `data` | `items`, `page`, `pageSize`, `totalCount` under `data` |
| TC-02 | AC-11 | Api.IntegrationTests | PASS `AC11_GetCustomers_PageSizeAboveMaximum_Returns400` | `pageSize` above the server maximum / list / observe | 400 |
| TC-03 | AC-13 | Api.IntegrationTests | PASS `AC13_GetCustomers_SearchTerm_MatchesNameOrEmail` | a term matching one customer's name / search / inspect items | only that customer |
| TC-04 | AC-13 | Api.IntegrationTests | PASS `AC13_GetCustomers_SearchTerm_MatchesNameOrEmail` (asserts the upper-cased term too) | the same term in different case against an email / search / inspect | matched case-insensitively |
| TC-05 | AC-13 (negative) | Api.IntegrationTests | PASS `AC13_GetCustomers_SearchMatchingNothing_ReturnsEmptyPageNotAnError` | a term matching nothing / search / inspect | empty `items`, non-zero `totalCount` context, envelope intact |

## Notes

The page-size cap is a denial-of-service control as much as a usability one. Without it, `pageSize=1000000` is a free way to make the server materialise the table.

## Open questions

None.

## Status evidence

Implemented in `Features/Customers/Queries/GetCustomers`.

AC-10 -> `AC10_GetCustomers_ReturnsPagedEnvelope`; AC-11 ->
`AC11_GetCustomers_PageSizeAboveMaximum_Returns400`; AC-13 ->
`AC13_GetCustomers_SearchTerm_MatchesNameOrEmail`, which also asserts case-insensitivity.

**Divergence:** the paged envelope field is `pageIndex`, not the `page` AC-10 names - the inherited
`PaginatedList<T>` shape, kept rather than renaming a type six features depend on. Recorded as `D4`
in `docs/superpowers/plans/EPIC-02-US-001-feat-03-customer-records/README.md`.

**Accepted risk:** AC-13's case-insensitivity comes from SQL Server's default collation, not from
the handler. The test pins the behaviour so a collation change fails loudly.

Run 2026-08-26: 192 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
