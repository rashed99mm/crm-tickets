# Task 1 — Ask for unassigned tickets

| Field | Value |
|---|---|
| Plan | [`implementation-plan.md`](../implementation-plan.md) — the whole plan; it is one flag |
| Feature | `MVP-12` Agent dashboard (backend half) |
| Criteria | `AC-82`, combining with `AC-33`, precedence against `AC-34` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/GetTicketsQuery.cs`
- `src/CustomerSupport.InternalApi/Controllers/TicketsController.cs`
- `tests/CustomerSupport.Tests/Integration/TicketLifecycleEndpointTests.cs`

No new endpoint, no migration, no new error code — as the plan specified.

## What changed

`bool Unassigned { get; init; }` on `GetTicketsQuery`, and in the handler:

```csharp
.WhereIf(request.Unassigned && !request.Mine, t => t.AssigneeId == null)
.WhereIf(assigneeId.HasValue, t => t.AssigneeId == assigneeId!.Value);
```

placed **before** the assignee filter so the `mine` precedence is read off the code rather than
inferred from which `WhereIf` happens to come last. `[FromQuery] bool unassigned = false` on
`TicketsController.GetAll`, documented with a `<param>` that says explicitly how it differs from
omitting `assigneeId`.

The rejected alternative — reading an empty `Guid` as "is null" — was not implemented. It is a magic
value, it would be silently wrong for a caller who passed `Guid.Empty` by accident, and OpenAPI
cannot describe it honestly.

## Test evidence

Three tests, each `[Trait("AC", "82")]`, added to the lifecycle suite because it already carries the
supervisor/agent/assignment fixture.

- `AC82_GetTickets_Unassigned_ReturnsOnlyTicketsNobodyHolds` — asserts **both** directions: the
  unheld ticket is present *and* the held one is absent, plus `OnlyContain(t => t.AssigneeId == null)`
  over the page. A filter that ignored the flag and returned the whole queue passes a presence-only
  assertion, which is exactly what it did before the change.
- `AC82_GetTickets_UnassignedCombinesWithStatus` — `unassigned=true&status=Open` conjoins (AC-33):
  the open-and-unheld ticket is present, the new-and-unheld and the open-but-held ones are not.
- `AC82_GetTickets_MineWinsOverUnassigned` — `mine=true&unassigned=true` as the agent returns the
  agent's own held ticket and not the unheld one.

Red first. Before the implementation:

```
Failed!  - Failed:     2, Passed:     1, Skipped:     0, Total:     3
```

Both failures were `to not contain {…}` — the whole queue came back, which is the failure mode the
two-directional assertion exists to catch.

**`AC82_GetTickets_MineWinsOverUnassigned` was green before the change**, because an ignored
`unassigned` and a correct precedence are indistinguishable from the outside. It is a regression
guard rather than a driver, and saying so is more useful than presenting three reds.

After:

```
Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3, Duration: 6 s
```

Full suite, against real LocalDB:

```
Passed!  - Failed:     0, Passed:   262, Skipped:     0, Total:   262, Duration: 54 s - CustomerSupport.Tests.dll (net10.0)
```

259 before, 262 after. No new build warnings; the CS8601/CS8604/CS1574 warnings in the output are
pre-existing and sit in `Infrastructure` and `Application` files this task did not touch.

## Deviations from the plan

None in substance. The plan's handler snippet is what shipped.

## The point of this task

This closes the spec's **open question** rather than leaving it for the frontend to discover.
`GetTicketsQuery.AssigneeId` is a `Guid?` in which *absent* means "any assignee" — the nullable
filter's own absence is already spoken for, so "is null" has nowhere to live. The flag is not
redundancy with `assigneeId`; it is the one predicate the nullable parameter structurally cannot
express. `AC-82`'s supervisor count has no other way to be asked for.
