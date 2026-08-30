# Task 2 — Read history, newest first, with actor names

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — US-022, tasks 2.1–2.2 |
| Feature | `FEAT-08` Ticket history |
| Criteria | `AC-50` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `tests/CustomerSupport.Tests/Integration/TicketLifecycleEndpointTests.cs`
- (the read itself: `src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQuery.cs`, from FEAT-04)

## Test evidence

- `AC50_TicketHistory_IsNewestFirstWithActorDisplayNames`
- `AC50_HistoryRow_StoresActorIdNotName`

Suite: **233 passed, 0 failed.**

## `AC-50`'s second half is the one with a design in it

The obvious way to render "Dana Support changed the status" is to store the name on the row. It
renders with no lookup, it survives the user being deleted, and it is wrong for two reasons:

1. **It freezes a name that changes.** Someone marries, or a typo in their profile is corrected, and
   every historical row still shows the old one.
2. **It duplicates personal data into an append-only table.** By construction nothing can ever
   correct those rows — the guard in `AC-49` refuses it — so a name written there is written
   permanently.

So the row holds `ActorId` and the name is a **read-time projection**.
`AC50_HistoryRow_StoresActorIdNotName` asserts that directly against the persisted entity, rather
than trusting that nobody adds the column later.

## Deviations from the plan

**1. No production code was written for this criterion.**
`GetTicketByIdQuery` already ordered newest-first and already resolved names, built in `FEAT-04`
because that feature needed a read endpoint to verify what it had created. Its record explicitly
declined to claim `AC-50`: *"a shape that happens to satisfy a criterion is not the same as a
criterion that has been proven."*

This task is the proof. It is worth being clear that the delivery here is the **test**, not the
feature — a record that implied otherwise would overstate the work.

## Carried forward

The name lookup is one call per **distinct** actor, through `IIdentityUserService`, because
`ApplicationUser` sits outside `IRepository<T>`'s `BaseEntity` constraint and there is no queryable
to join. For a handful of rows and two or three actors that is a few reads. It **would** become an
N+1 if a ticket's history ever grew large, and `FEAT-04`'s record flagged it as the point to
revisit. Nothing in this phase changed that, and nothing in this phase made it worse.
