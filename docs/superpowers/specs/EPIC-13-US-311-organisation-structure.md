# Organisation structure — departments and branches

**Sprint:** 7 · **Feature:** `FEAT-16` · **Stories:** `US-301`–`US-305`, `US-307`, `US-308` ·
**Epic:** [`EPIC-12` Platform](../../requirements/epics/EPIC-12-platform.md)

## Current implementation addendum

The original baseline below describes the grouping-only scope approved first. The current
implementation also completes the organisation flow: users can be assigned a department, branch,
and team; customer and ticket creation inherit the acting user's organisation; ticket assignment
inherits the target agent's organisation; and branch-assigned users receive branch-scoped ticket
and customer list/detail results. Persisted `ApplicationUser.BranchId` is the visibility source,
while `DepartmentId` remains grouping and routing metadata. See
[`US-306`](../../requirements/user-stories/US-306-branch-scoped-queries.md) and the
[as-built execution plan](../plans/EPIC-12-US-306-branch-scoped-queries/as-built-execution-plan.md).

## Problem

Nothing in the platform groups users, tickets, customers or categories by organisational unit.
Two groupings are needed as a foundation for later features (reporting by department, branch-aware
dashboards): `Department` and `Branch`.

## Assumptions

A1. **This pass adds the grouping, not visibility restriction.** `US-306` (branch-scoped query
    filters) is cut — the project's open-question register lists `OQ-5` ("Do branches restrict
    visibility or only group?") as **unresolved**, and `US-306`'s own `BR-21` already assumes the
    "restrict" answer. Building it now would silently decide a live product ambiguity rather than
    surface it. `DepartmentId`/`BranchId` land on every relevant table so a future decision has
    something to filter on; no query changes behaviour based on them yet.

A2. **`US-309` (department admin UI) is cut for time**, not for a design reason — recorded as a cut,
    not silently dropped. The CRUD API this pass ships is real and independently testable; a screen
    for it is the natural next increment.

A3. **All new FK columns are nullable**, per the stories' own notes — existing rows have no
    department/branch, and a `NOT NULL` column would need a value on every historical row.

A4. **Soft delete for Department/Branch reuses `BaseEntity.SoftDelete()`**, the mechanism every other
    entity in this codebase already uses (`Customer`, `Ticket`, `Asset`, …), not a bespoke
    `IsActive` toggle each story's SQL sketch separately proposes. `IsActive` becomes a computed
    read of `!IsDeleted` rather than a second, independently-settable flag — two flags for one fact
    is what let `MVP-02`'s `IsActive` vs `LockoutEnabled` drift happen once already.

A5. **`ManagerId` (Department) has no FK constraint to `AspNetUsers`.** Every existing FK to the
    identity user table in this codebase (`CustomerNote.AuthorId`, `TicketHistory.ActorId`,
    `TicketMessage.SenderId`) points at a *required* actor. A department manager is optional and
    unvalidated this pass — enforcing "must be a real user, must hold a role that can manage" is
    exactly the kind of authorization design `FEAT-07`'s spec gave real thought to, and `US-301`
    raises none of it. Left as a bare nullable `Guid` rather than inventing that design as a side
    effect of an unrelated entity.

A6. **The seed uses well-known GUIDs** (`00000000-0000-0000-0000-000000000001` for both), as
    `US-305` specifies, so a future story (or `US-306`, if `OQ-5` resolves) can reference them
    without a lookup.

## Out of scope

- `US-306` — branch-scoped query filters (blocked on `OQ-5`, A1).
- `US-309` — department management screen (cut for time, A2).
- A branches admin screen (never specced; would be the same cut as `US-309` for the same reason).
- Assigning any existing user, ticket, customer or category to a department/branch. The columns
  exist and default to `null`; nothing in this pass populates them beyond the CRUD endpoints
  themselves creating/updating `Department`/`Branch` rows.
- Department/branch selection on ticket creation or the customer/user forms.
- Manager validation (A5).

## Acceptance criteria

AC-115. Given a `Department` is created with a name, when persisted, then `Id`, `Name`, `ManagerId`
(nullable), `IsActive` (computed `!IsDeleted`) and the audit fields are present and correctly typed.

AC-116. Given a `Branch` is created with a name, when persisted, then `Id`, `Name`, `Region`
(nullable), `Timezone` (defaults to `"UTC"`), `IsActive` and the audit fields are present and
correctly typed.

AC-117. Given the migration is applied, then `Users`, `Tickets` and `Categories` each carry a
nullable `DepartmentId` FK to `Departments`, and `Users`, `Tickets` and `Customers` each carry a
nullable `BranchId` FK to `Branches`. No existing row's data is altered.

AC-118. Given the database is seeded, then exactly one `Department` (`"General"`) and one `Branch`
(`"Head Office"`) exist at the well-known ids, both active.

AC-119. Given an authenticated Admin, when calling `GET /api/Departments`, `POST /api/Departments`,
`PUT /api/Departments/{id}`, `DELETE /api/Departments/{id}`, then each succeeds with the documented
shape (200/201/200/200 — see Design for why 200 not 204).

AC-120. Given an authenticated caller who is not an Admin, when calling any mutating Departments or
Branches route, then the response is 403.

AC-121. Given a name that is empty or exceeds 200 characters, when creating or updating a Department
or a Branch, then the response is 400 keyed to `Name`.

AC-122. Given an unknown id, when updating, deleting, or fetching by id, a Department or a Branch,
then the response is 404.

AC-123. Given an authenticated Admin, when calling `GET /api/Branches`, `POST /api/Branches`,
`PUT /api/Branches/{id}`, `DELETE /api/Branches/{id}`, then each succeeds with the documented shape.

## Design

### Backend: Domain

Two new `BaseEntity` subclasses, `Department` and `Branch`, in
`CustomerSupport.Domain/Entities/Organisation/`. Each carries a private-setter `Create`/`Update`
pair following `Customer`'s shape (validated name, trimmed, max 200 chars) and relies on
`BaseEntity.SoftDelete()` for AC-121's deactivation rather than a bespoke flag (A4). `Department`
additionally carries `ManagerId` (`Guid?`, unvalidated — A5); `Branch` carries `Region` (`string?`)
and `Timezone` (`string`, defaults `"UTC"`, max 100 chars).

`Ticket`, `Customer`, `Category` gain nullable `DepartmentId`/`BranchId` properties per US-303/304
(Category gets `DepartmentId` only; Customer gets `BranchId` only — per those stories' own table
lists). `ApplicationUser` (Identity, already outside `IRepository<T>`'s constraint) needs both;
added as plain nullable `Guid` properties with no navigation collection, matching how
`ApplicationUser` already carries no navigation properties to its own dependents elsewhere in this
codebase.

### Backend: Application

Standard CQRS pairs, matching `CreateCustomerCommand`/`GetCustomersQuery`'s shape exactly:
`CreateDepartmentCommand`, `UpdateDepartmentCommand`, `DeactivateDepartmentCommand`,
`GetDepartmentsQuery` (paginated), `GetDepartmentByIdQuery`, and the same four for `Branch`. Each
validator follows `CreateCustomerCommandValidator`'s pattern (`RuleFor(x => x.Name)` directly, never
through a lambda helper).

### Backend: API

Two controllers, `DepartmentsController` and `BranchesController`, `[Authorize(Policy = "Admin")]`
on every mutating action (AC-120) and `[Authorize(Policy = "Authenticated")]` on the reads — matching
the existing split between `TicketsController`'s open reads and `TicketsController.Assign`'s
`Supervisor`-gated mutation. `DELETE` returns **200 with the envelope**, not 204, following this
codebase's own established convention (`CustomersController.Delete`) — a bare 204 carries no code or
bilingual message, which this project's response contract does not allow silently.

### Data model

One migration: `Departments`, `Branches` tables, plus `DepartmentId`/`BranchId` columns added to
`Users` (both), `Tickets` (both), `Categories` (`DepartmentId`), `Customers` (`BranchId`) — all
nullable, all `Restrict` on delete (matching this codebase's existing FK convention, e.g.
`TicketMessageConfiguration`). Seed data via `HasData` in the entity configurations (US-305's own
guidance), at the well-known ids (A6).

### Error behavior

New codes `DEPARTMENT_NOT_FOUND`, `DEPARTMENT_CREATED`, `DEPARTMENT_UPDATED`,
`DEPARTMENT_DEACTIVATED`, `BRANCH_NOT_FOUND`, `BRANCH_CREATED`, `BRANCH_UPDATED`,
`BRANCH_DEACTIVATED`, plus `Validation.ORG_NAME_REQUIRED`/`ORG_NAME_MAX_LENGTH`, each with a
bilingual `Resources.yaml` pair — no new error-handling mechanism.
