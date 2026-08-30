# MVP-12 — Agent dashboard · **backend** implementation plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Date:** 2026-08-26
**Spec:** [`../../specs/EPIC-08-US-606-agent-dashboard.md`](../../specs/EPIC-08-US-606-agent-dashboard.md)
**Criteria:** supports `AC-82`
**Size:** one filter flag. Deliberately small — the dashboard is composed from the existing queue
endpoint, and this is the single thing that endpoint cannot currently express.

## The gap

`AC-82` needs "tickets nobody is working on". `GetTicketsQuery.AssigneeId` is a `Guid?`, where
**absent means "no filter"**, not "assignee is null". There is no way to ask for unassigned tickets.

Rejected alternative: treating `assigneeId=00000000-0000-0000-0000-000000000000` as "is null". That
is a magic value, it would be silently wrong for anyone passing an empty Guid by accident, and it
cannot be documented honestly in OpenAPI.

## Code plan

**Edit:** `src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/GetTicketsQuery.cs`

```csharp
/// <summary>
/// Only tickets nobody holds (AC-82). Distinct from an absent <see cref="AssigneeId"/>, which means
/// "any assignee" — a nullable filter cannot express "is null" without a flag.
/// </summary>
public bool Unassigned { get; init; }
```

In the handler, **before** the `mine` resolution so precedence is explicit:

```csharp
// `mine` is about the caller; `unassigned` is about nobody. If both arrive, `mine` wins —
// a caller asking for their own unassigned tickets is asking for the empty set, and honouring
// that literally is less useful than honouring the more specific intent.
var filter = PredicateBuilder.True<Ticket>()
    …
    .WhereIf(request.Unassigned && !request.Mine, t => t.AssigneeId == null)
    .WhereIf(assigneeId.HasValue, t => t.AssigneeId == assigneeId!.Value);
```

**Edit:** `src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` — add
`[FromQuery] bool unassigned = false` to `GetAll`, pass it through, and document it with `<param>`.

No new endpoint, no migration, no new error code.

## Tests

**Edit:** `tests/CustomerSupport.Tests/Integration/TicketLifecycleEndpointTests.cs` (it already has
the supervisor/agent/assignment fixture).

| Test | Criterion |
|---|---|
| `AC82_GetTickets_Unassigned_ReturnsOnlyTicketsNobodyHolds` | `AC-82` |
| `AC82_GetTickets_UnassignedCombinesWithStatus` | `AC-82`, `AC-33` |
| `AC82_GetTickets_MineWinsOverUnassigned` | precedence, stated above |

The first must assert **both** directions — an unassigned ticket is present *and* an assigned one is
absent. A filter that returns everything passes a presence-only assertion.

## Definition of done

Three tests naming `AC-82` · `dotnet test` green with output pasted · 0 errors, no new warnings ·
task records in `tasks/`. **Do not touch `frontend/`.**
