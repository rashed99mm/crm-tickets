# ADR 0006 — Soft delete by default, with filtered unique indexes

- **Status:** Accepted
- **Date:** 2026-08-24

## Context

Entities need `IAuditable` and `ISoftDeletable` base types. Support records in particular should not
be destroyable by a single click — S1 assumption A10 already refuses to delete a customer who has
tickets.

Soft deletion interacts with two existing acceptance criteria in a way that is easy to miss:

- **AC-16** requires a deleted customer to be gone from listings.
- **AC-9** requires customer email addresses to be unique.

A plain unique index plus soft deletion makes these contradictory: the deleted row still occupies
the index, so re-creating a customer with a previously-used address fails with a conflict, pointing
at a record the user can no longer see. That is a confusing bug and a support call nobody can
resolve from the UI.

## Decision

`ISoftDeletable` entities are never physically deleted. A `SaveChangesInterceptor` rewrites a
`Deleted` entry into a `Modified` one, setting `IsDeleted`, `DeletedAtUtc` and `DeletedBy`. A
global query filter applied by reflection in `OnModelCreating` excludes deleted rows from every
query.

**Unique indexes on soft-deletable entities are filtered:** `WHERE IsDeleted = 0`. A deleted
customer's email address is reusable, and AC-9 and AC-16 both hold.

## Alternatives considered

| Option | Why it lost |
|---|---|
| **Hard delete** | Satisfies both criteria trivially and needs no interceptor. It lost because support history is the product's value — a mis-click destroying a customer's record with no recovery is a worse failure than the complexity of soft deletion. |
| **Soft delete with an unfiltered unique index** | The default if nobody notices. Creates the contradiction above: a conflict against an invisible row. |
| **Soft delete plus an email-uniqueness check in the handler instead of an index** | Moves the rule into application code where a race between two requests can defeat it. A database constraint is the only place uniqueness can actually be guaranteed. |
| **Anonymise on delete** (overwrite PII, keep the row) | Genuinely better for data-protection obligations, and worth revisiting if this became a real product. Out of scope for S1 and would need a separate spec about what to retain. |

## Consequences

- Deleted data is recoverable, and the audit trail survives a deletion.
- Every query carries the filter automatically. **`IgnoreQueryFilters()` bypasses it**, which is
  correct for an admin view and a data leak anywhere else — worth watching for in review.
- The global filter must be applied by reflection over entity types, not per entity. A hand-written
  filter per entity is one forgotten line away from exposing deleted rows.
- Uniqueness is now conditional. A developer adding a unique index later must remember the filter,
  or reintroduce the contradiction. This is the fragile part of the decision and the reason it is
  written down.
- Filtered indexes are SQL Server syntax. Moving to PostgreSQL would need partial indexes —
  equivalent, but not a no-op migration.
- Rows accumulate. Irrelevant at assessment scale; a purge policy would be needed for a real
  deployment.
