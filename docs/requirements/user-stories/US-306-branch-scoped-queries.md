# US-306 · Branch-Scoped Query Filters

| Field | Value |
|---|---|
| **Story** | `US-306` |
| **Epic** | [EPIC-12 Platform](../epics/EPIC-12.md) |
| **Feature** | [`FEAT-16` Organisation structure](../delivery-plan.md#feat-16--organisation-structure) |
| **Layer** | Backend |
| **Ships with** | — |
| **Actor** | Supervisor |
| **Priority** | P1 |
| **Sprint** | [7 — Organisation structure](../delivery-plan.md#sprint-7-organisation-structure) · Slice S8 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-12.8 |
| **Spec criteria** | AC-17 |
| **Depends on** | [US-304](./US-304-branch-foreign-keys.md) |

## Story

**As a supervisor**, **I want** to see only my branch's data, **so that** visibility is scoped.

## Business rules

- BR-21 — Branch-scoped data visibility: users with a `BranchId` may only see records belonging to their branch. Users without a `BranchId` (admin-level) see all records.

## Acceptance criteria

#### AC1 — Branch-scoped ticket and customer queries (AC-17)

Given a branch user is authenticated, when querying tickets or customers, then only records matching the user's `BranchId` are returned.

#### AC2 — Unscoped admin visibility (AC-17)

Given a user with no `BranchId` is authenticated, when querying tickets or customers, then all records are returned regardless of branch.

## SQL tables

None — this story adds query filters to existing `Tickets` and `Customers` queries.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-17 | Integration | `AC17_BranchUser_SeesOnlyTicketsAndCustomersInOwnBranch` | Given a user with `BranchId = X`, when tickets or customers are queried, then only rows with `BranchId = X` are returned | Filtered result set |
| TC-02 | AC-17 | Integration | `BranchUserSeesOnlyOwnBranchCustomers` | Given a user with `BranchId = X`, when customers are queried, then only customers with `BranchId = X` are returned | Filtered result set |
| TC-03 | AC-17 | Integration | `AdminUserSeesAllBranches` | Given a user with `BranchId = NULL`, when tickets or customers are queried, then all records are returned | Unfiltered result set |
| TC-04 | AC-17 | Unit | `BranchFilterNotAppliedWhenNull` | Given `BranchId` is null on the calling user, when the filter expression is built, then no branch predicate is applied | No filter clause generated |

## Notes

- The filter should be implemented as a global query filter or a query specification, not inline in controllers.
- Should be composable with existing filters (e.g. department, status).
- `BR-21` is the authoritative business rule from the brief.

## Open questions

None.

## Status evidence

Implemented in the application query handlers. The authenticated user's persisted `ApplicationUser.BranchId`
is the scope source; no JWT claim is required. `CreateCustomerCommandHandler` assigns the acting user's
branch, `CreateTicketCommandHandler` inherits it, and `AssignTicketCommandHandler` rehomes the ticket to
the target agent's branch. `GetTicketsQueryHandler`, `GetTicketByIdQueryHandler`, `GetCustomersQueryHandler`,
and `GetCustomerByIdQueryHandler` apply the branch predicate. Coverage is
`OrganisationStructureEndpointTests.AC17_BranchUser_SeesOnlyTicketsAndCustomersInOwnBranch`.

Departments remain available for grouping and assignment through the existing department CRUD and user
organisation assignment flow; department-based visibility is not part of the current authorization policy.

Historical blocker note: `FEAT-16`'s original task record was blocked on `OQ-5`
(an unresolved product-level question that has now been resolved for this implementation as
persisted-user branch scoping). The old plan's claim that the columns were never populated is no
longer current.

Status is set from what is committed and executed, never from what is planned.
