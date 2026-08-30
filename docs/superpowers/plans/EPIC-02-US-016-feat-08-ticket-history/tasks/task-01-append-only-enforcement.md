# Task 1 — Prove the append-only guard, and audit the route surface

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — US-121, tasks 1.1–1.5 |
| Feature | `FEAT-08` Ticket history |
| Criteria | `AC-48`, `AC-49`, `BASE-14` |
| Status | `done` |
| Commit | uncommitted — working tree |
| Decision | [ADR-0010](../../../../adr/0010-append-only-history-enforced-by-a-savechanges-guard.md) |

## Files

- `tests/CustomerSupport.Tests/Integration/TicketLifecycleEndpointTests.cs`
- (enforcement itself: `src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs`, unchanged since Phase 0)

## Test evidence

- `AC48_EveryTicketEvent_PersistsItsOwnHistoryRow` — one ticket taken through create, assign,
  reassign, status change, resolve and reopen, asserting the exact six-row timeline in order, each
  row carrying an actor and a UTC timestamp
- `AC49_UpdatingAHistoryRow_IsRefused`
- `AC49_DeletingAHistoryRow_IsRefused`
- `AC49_NoEndpointExposesHistoryMutation`

Suite: **233 passed, 0 failed.**

## What this task actually delivered

**No production code.** The guard has existed since Phase 0 and the aggregate has appended its own
rows since Phase 0. What did not exist was evidence.

ADR-0010 argued the `SaveChanges` guard beats absent columns partly *because it is testable* — an
assertion carried for three phases. These tests are what turn it into a fact:

- The **update** test is the one absent columns could never have covered. Dropping
  `IsDeleted`/`ModifiedAt` prevents a soft delete; it does nothing about an `UPDATE` that rewrites
  `ToValue`, which is the falsification that actually matters for an audit trail.
- The **delete** test covers the case absent columns *would* have handled, so the guard is shown to
  be a superset rather than a trade.

## The surface audit, and why it looks like it does nothing

`AC49_NoEndpointExposesHistoryMutation` resolves `EndpointDataSource` from the running host,
filters routes whose pattern mentions history, and asserts none carries `POST`, `PUT`, `PATCH` or
`DELETE`.

Today it passes trivially — there is no history route at all. **That is the intended state, not a
weak test.** `US-121` TC-02 asked for a surface audit because `AC-49` says "no endpoint updates or
deletes a history row, **and none is exposed to do so**" — a claim about absence, which no ordinary
endpoint test can make. It is a tripwire for the future `HistoryController` that gets added in a
hurry, and it will fail the moment one appears.

## Deviations from the plan

**1. `AC-48`'s evidence is one long test rather than five short ones.**
The plan implied a case per event. A single ticket walked through every transition asserts the
timeline **as a sequence** — that the rows accumulate in order, that a reopen is recorded as
`Reopened` rather than `StatusChanged`, and that a reassignment names the previous holder. Five
isolated tests would each pass while the ordering between them went wrong.

**2. The guard needed no change, but it had already been proven fallible.**
During `FEAT-06` it refused a legitimate append — a mis-tracked insert EF had marked `Modified`.
The guard behaved as designed; it simply cannot tell that case from a real mutation. Recorded here
because it qualifies `AC-49`'s enforcement in a way ADR-0010 does not mention.
