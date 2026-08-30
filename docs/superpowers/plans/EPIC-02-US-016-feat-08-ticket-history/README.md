# FEAT-08 — Ticket history · task record

**Plan:** [`implementation-plan/implementation-plan.md`](./implementation-plan.md)
**Executed:** 2026-08-26
**Status:** delivered — and ADR-0010's central claim is finally under test

## Evidence

```
dotnet test CustomerSupport.slnx
Passed!  - Failed: 0, Passed: 233, Skipped: 0, Total: 233, Duration: 58 s
```

## Tasks

| # | Task | Criteria | Commit | Status |
|---|---|---|---|---|
| [01](./tasks/task-01-append-only-enforcement.md) | Prove the append-only guard, and audit the route surface | AC-48, AC-49 | uncommitted | `done` |
| [02](./tasks/task-02-read-history.md) | Read history newest-first with actor names | AC-50 | uncommitted | `done` |

## Criteria delivered

| `AC-n` | Test naming it |
|---|---|
| AC-48 | `AC48_EveryTicketEvent_PersistsItsOwnHistoryRow` — all five change types in one ticket's timeline, plus `AC48_CreateTicket_PersistsOneCreatedHistoryRow` from FEAT-04 |
| AC-49 | `AC49_UpdatingAHistoryRow_IsRefused`, `AC49_DeletingAHistoryRow_IsRefused`, `AC49_NoEndpointExposesHistoryMutation` |
| AC-50 | `AC50_TicketHistory_IsNewestFirstWithActorDisplayNames`, `AC50_HistoryRow_StoresActorIdNotName` |

## The debt this feature was created to settle

[ADR-0010](../../../adr/0010-append-only-history-enforced-by-a-savechanges-guard.md) traded a
structural guarantee — absent columns — for a `SaveChanges` guard, and justified it on three
grounds. The third was that the guard is **directly testable**, "which `AC-49` asks for".

That had been an assertion since Phase 0. `AC49_UpdatingAHistoryRow_IsRefused` and
`AC49_DeletingAHistoryRow_IsRefused` now substantiate it: a row is loaded, mutated, saved, and the
save throws. The ADR's claim stands and no amendment is needed.

## Deviations from the plan

**D1 — The guard had already produced a false refusal, in FEAT-06.**
Not a deviation in this feature's work, but it belongs in this feature's record because it is about
`AC-49`'s mechanism. A client-assigned `Id` on an appended row made EF mark a brand-new row
`Modified`, and the guard correctly refused to save it — breaking every status change with a 500.

**The guard did exactly what it was designed to do and was still wrong**, because it cannot
distinguish a mis-tracked insert from a genuine mutation. ADR-0010 did not anticipate that failure
mode. It is now noted in `TicketHistory` itself, where the next person to reintroduce the `Id`
assignment will see it. Full account in
[FEAT-06 task 3](../EPIC-02-US-016-feat-06-ticket-lifecycle/tasks/task-03-reopen-and-concurrency.md).

**D2 — `AC49_NoEndpointExposesHistoryMutation` is a test about what does not exist.**
`US-121` TC-02 asked for a surface audit and no ordinary endpoint test can express one. It resolves
`EndpointDataSource` from the running host, filters routes whose pattern mentions history, and
asserts none carries a mutating verb.

It passes trivially today — there is no history route at all — and that is the point: it is a
tripwire for a future `HistoryController` that nobody reviews carefully, not a demonstration of
current behaviour.

## Nothing was built for AC-50 except its tests

`GetTicketByIdQuery` already returned entries newest-first with actor display names, delivered in
`FEAT-04` task 6 and explicitly **not claimed** there because a shape that happens to satisfy a
criterion is not a criterion that has been proven. This feature is where the tests that name it
were written.
