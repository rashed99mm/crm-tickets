# ADR 0010 — Enforce append-only ticket history with a SaveChanges guard, not with absent columns

- **Status:** Accepted
- **Date:** 2026-08-25

## Context

`AC-48` requires a history row for every ticket creation, assignment, reassignment and status change.
`AC-49` requires that history be **append-only**: no endpoint updates or deletes a row, and none is
exposed to do so.

The original schema design ([`EPIC-12-US-000-s1-schema.md`](../superpowers/specs/EPIC-12-US-000-s1-schema.md),
now superseded) enforced this structurally. `TicketHistory` was given no `IsDeleted`, no
`ModifiedAtUtc` and no `ModifiedBy` columns at all, and `erd.md` states the reasoning plainly under
`BR-5`: "the absence is the enforcement, because there is no code path that could populate them."

Adopting the support platform as the baseline ([ADR-0009](0009-adopt-the-support-platform-as-the-crm-baseline.md))
put that approach under pressure. The platform's data access runs through one generic repository:

```csharp
public interface IRepository<T> where T : BaseEntity
```

`BaseEntity` carries the audit and soft-delete column set as concrete properties — it is a base
class, not a composable interface. So an entity that keeps those columns absent is an entity outside
the repository's type constraint, and every read and write of it needs a bespoke port declared in
`Domain`, a bespoke implementation in `Infrastructure`, and hand-written paging that duplicates what
`GetPagedAsync` already does.

Structural enforcement also turns out to be narrower than it first appears. Absent soft-delete
columns prevent a *delete*. They do not prevent an `UPDATE` — nothing stops a future handler loading
a history row and rewriting its `ToValue`, which is the falsification that actually matters for an
audit trail. And "no code path exists" is a claim about code that does not exist yet; it is not a
test, and `AC-49` is a criterion that wants one.

## Decision

`TicketHistory` derives from `BaseEntity` like every other entity, and append-only is enforced by an
explicit guard at the single write point — `AppDbContext.SaveChangesAsync` — which throws if any
tracked `TicketHistory` entry is in the `Modified` or `Deleted` state.

The guard runs **before** the audit pass that rewrites `Deleted` into `Modified`, so both a direct
delete and a soft delete are refused, and each is refused by name.

## Alternatives considered

| Option | Why it lost |
|---|---|
| **Keep `TicketHistory` outside `BaseEntity`, as originally designed** | Genuinely the stronger guarantee against deletion, and it was the plan. It costs a bespoke repository port, its implementation, and its own paging — for one entity — and it still leaves `UPDATE` open. That is a real trade, not a strawman: what is lost is that a soft delete becomes *possible-but-refused* rather than *inexpressible*. |
| **A database trigger refusing `UPDATE`/`DELETE` on the table** | The strongest enforcement, and it survives an ORM bypass, which the guard does not. Rejected because it puts a business rule in a place no test in this solution reads, no migration review would catch, and no developer would look for — and because `BR-3` already establishes that transition rules live in the domain, not the database. Worth revisiting if a second writer ever reaches this table. |
| **A repository wrapper that hides `Update` and `Remove` for this type** | Enforces at the wrong altitude: a caller with the `DbContext` bypasses it entirely, and this codebase's handlers do reach `AppDbContext` through `IUnitOfWork`. |
| **Convention alone — simply never write the update path** | This is what `AC-49` already asks to be *proven*. An unenforced convention is what the criterion exists to distrust. |

## Consequences

**Easier.** `TicketHistory` uses the same repository, paging and query-filter machinery as every
other entity, so the newest-first read for `AC-50` is `GetPagedAsync` and nothing more. The rule
becomes directly testable — a test that loads a row, mutates it, saves, and asserts the throw is the
evidence `AC-49` asks for, and it fails loudly the day someone adds an update path.

**Harder.** The columns exist, so the schema no longer documents the rule to a reader of the DDL;
`erd.md`'s `BR-5` note and this ADR are now the only places that say it. Anyone bypassing
`AppDbContext.SaveChangesAsync` — raw SQL, a second `DbContext`, a bulk-update extension — bypasses
the rule. That is the concrete cost of moving enforcement out of the schema, and it is why the
trigger option above stays on the table if this data is ever written by anything but this
application.

**Hard to reverse.** Not very, and that is deliberate: dropping the four columns later is a migration
plus a bespoke port, and the guard can stay alongside a trigger rather than being replaced by one.
