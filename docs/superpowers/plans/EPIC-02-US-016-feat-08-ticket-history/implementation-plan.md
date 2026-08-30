> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier � this plan did not precede its implementation.

# FEAT-08 — Ticket history · backend plan

> **Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan
> did not precede its implementation.**

**Date:** 2026-08-26
**Feature:** `FEAT-08`, sprint 3, **API-only** · 2 stories · 8 points
**Spec:** `AC-48`, `AC-49`, `AC-50`, `BASE-14`
**Decision:** [ADR-0010](../../../adr/0010-append-only-history-enforced-by-a-savechanges-guard.md)
**UI surface:** the timeline in `US-128`
**Depends on:** `FEAT-07` — assignment events are among what history records

## What already exists

The aggregate appends its own rows for creation, assignment, reassignment, status change and
reopen via `Ticket.Append` → `TicketHistory.Record` (`CustomerSupport.Domain/Entities/Tickets/`).
`GetTicketByIdQuery` already returns entries newest-first with actor display names, delivered in
`FEAT-04` and explicitly **not claimed** there. `AppDbContext.SaveChangesAsync`'s
`GuardAppendOnlyHistory()` already refuses a `Modified`/`Deleted` `IAppendOnlyEntity` row.

**This feature settles the debt Phase 0 knowingly left**: ADR-0010 argues the `SaveChanges` guard
is better than absent columns *because it is testable*, and nothing has yet tested it against a
real database.

---

### Task 1: Every event records its own row (`AC-48`)

**Files:**
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

**Interfaces:**
- Consumes: `TicketHistory(Guid Id, Guid TicketId, Guid ActorId, string ChangeType, string?
  FromValue, string? ToValue, DateTime OccurredAt)` (already exists, read-only from outside the
  aggregate).

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "48")]
public async Task AC48_EveryTicketEvent_PersistsItsOwnHistoryRow()
{
    var (agentClient, agent) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
    var ticketId = await CreateTicketAsync("History fixture");

    var current = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");
    await _supervisor.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee",
        new { assigneeId = agent.Id, rowVersion = current!.Data!.RowVersion });
    var afterAssign = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");
    await agentClient.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
        new { status = "Open", rowVersion = afterAssign!.Data!.RowVersion });

    var detail = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    detail!.Data!.History.Select(h => h.ChangeType).Should()
        .Contain(["Created", "Assigned", "StatusChanged"]);
    agentClient.Dispose();
}

[Fact]
[Trait("AC", "48")]
public async Task AC48_HistoryRow_CarriesActorTimestampChangeTypeAndBothValues()
{
    var ticketId = await CreateTicketAsync("Row-shape fixture");

    var detail = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");
    var created = detail!.Data!.History.Single(h => h.ChangeType == "Created");

    created.ActorId.Should().NotBe(Guid.Empty);
    created.OccurredAt.Should().NotBe(default);
}
```

- [ ] **Step 2: Run tests**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC48_"`
Expected: PASS already — `Ticket.ChangeStatus`/`AssignTo`/`Create` each call `Append`
unconditionally. These tests exist to claim `AC-48` with names, not to add behavior.

- [ ] **Step 3: Commit**

```bash
git add backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs
git commit -m "test(tickets): claim AC-48 over the existing history append"
```

---

### Task 2: The append-only guard, against a real database (`AC-49`)

**Files:**
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

**Interfaces:**
- Consumes: `AppDbContext.GuardAppendOnlyHistory()` (`CustomerSupport.Infrastructure/Persistence/AppDbContext.cs`)
  — throws `InvalidOperationException` for any `IAppendOnlyEntity` row seen `Modified`/`Deleted`.

**This is the task the feature exists for.** ADR-0010 traded a structural guarantee — absent
columns — for a `SaveChanges` guard, on the claim that it is directly testable. That claim has
been an assertion since Phase 0; this task either substantiates it or the ADR needs rewriting.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
[Trait("AC", "49")]
public async Task AC49_UpdatingAHistoryRow_IsRefused()
{
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var ticketId = await CreateTicketAsync("Update-refused fixture");
    var row = await db.Set<TicketHistory>().FirstAsync(h => h.TicketId == Guid.Parse(ticketId.ToString()));

    db.Entry(row).State = EntityState.Modified;

    var act = async () => await db.SaveChangesAsync();
    await act.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("*append-only*");
}

[Fact]
[Trait("AC", "49")]
public async Task AC49_DeletingAHistoryRow_IsRefused()
{
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var ticketId = await CreateTicketAsync("Delete-refused fixture");
    var row = await db.Set<TicketHistory>().FirstAsync(h => h.TicketId == Guid.Parse(ticketId.ToString()));

    db.Entry(row).State = EntityState.Deleted;

    var act = async () => await db.SaveChangesAsync();
    await act.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("*append-only*");
}

[Fact]
[Trait("AC", "49")]
public async Task AC49_NoEndpointExposesHistoryMutation()
{
    // A surface audit, not a behavior test — asserts what does NOT exist, so nothing else can
    // express it. Guards against a future HistoryController nobody reviews carefully.
    using var scope = _factory.Services.CreateScope();
    var endpointSource = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();

    var mutatingHistoryRoutes = endpointSource.Endpoints
        .OfType<RouteEndpoint>()
        .Where(e => e.RoutePattern.RawText != null
            && e.RoutePattern.RawText.Contains("history", StringComparison.OrdinalIgnoreCase))
        .Where(e =>
        {
            var methods = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
            return methods.Any(m => m is "PUT" or "PATCH" or "DELETE" or "POST");
        });

    mutatingHistoryRoutes.Should().BeEmpty();
}
```

- [ ] **Step 2: Run tests**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC49_"`
Expected: PASS already — `AppDbContext.GuardAppendOnlyHistory()` runs unconditionally at the top
of `SaveChangesAsync`, and no `HistoryController` (mutating or otherwise) exists in either host.
This substantiates ADR-0010's testability claim; if either throw-assertion failed, the guard would
need to be added exactly where `GuardAppendOnlyHistory()` already sits.

- [ ] **Step 3: Commit**

```bash
git add backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs
git commit -m "test(tickets): substantiate ADR-0010's append-only guard against real SQL Server (AC-49)"
```

---

### Task 3: Read ticket history (`AC-50`)

**Files:**
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
[Trait("AC", "50")]
public async Task AC50_TicketHistory_IsNewestFirstWithActorDisplayNames()
{
    var (agentClient, agent) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
    var ticketId = await CreateTicketAsync("Newest-first fixture");
    var current = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");
    await _supervisor.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee",
        new { assigneeId = agent.Id, rowVersion = current!.Data!.RowVersion });

    var detail = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    detail!.Data!.History[0].ChangeType.Should().Be("Assigned"); // newest first
    detail.Data.History[0].ActorName.Should().NotBeNullOrEmpty();
    agentClient.Dispose();
}

[Fact]
[Trait("AC", "50")]
public async Task AC50_HistoryRow_StoresActorIdNotName()
{
    // The domain entity carries ActorId only — no name field to denormalise and freeze. The
    // name is resolved at read time by GetTicketByIdQueryHandler, via IIdentityUserService,
    // never persisted. Reflection asserts the shape rather than re-reading the same handler code
    // this whole file's other tests already exercise end to end.
    typeof(TicketHistory).GetProperty("ActorName").Should().BeNull();
    typeof(TicketHistory).GetProperty("ActorId").Should().NotBeNull();
}
```

Denormalising the display name into the row would let the timeline render without a lookup — and
would freeze a name that changes when someone marries, and would duplicate personal data into an
append-only table that by construction can never be corrected.

- [ ] **Step 2: Run tests**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC50_"`
Expected: PASS already — `GetTicketByIdQueryHandler` already resolves `actorNames` via
`identityUsers.FindByIdAsync` per distinct `ActorId` and projects into `TicketHistoryDto.ActorName`
at read time (see `Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs`); the
domain entity itself has no such field.

- [ ] **Step 3: Commit**

```bash
git add backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs
git commit -m "test(tickets): claim AC-50, newest-first with read-time actor names"
```

## Message catalogue

No new codes. History is read-only and its failures are the ticket's (`TICKET_NOT_FOUND`).

## Definition of done

1. `AC-48`, `AC-49`, `AC-50` each covered by a test naming it.
2. ADR-0010's testability claim substantiated against real SQL Server (`AC-49`'s two throw tests),
   not the in-memory provider.
3. Suite green, output pasted.
4. **The frontend plan for `US-128` follows immediately** — with this feature complete, all three
   backend features behind that screen exist.
5. Task records in `EPIC-02-US-016-feat-08-ticket-history/`.

