# US-607 Live Queue: Implementation Plan

> **Disclosure (added 2026-08-27):** Rewritten to carry real, code-bearing Task sections. The live
> queue described here is NOT SHIPPED; the code below designs a real-time ticket queue using the
> codebase's existing SignalR hub infrastructure in `CustomerSupport.Api.Shared` (per `ADR-0008`).

**Story:** `US-607` · **Spec:** `docs/superpowers/specs/EPIC-08-US-606-reporting.md` · **Status:** NOT SHIPPED

## AC mapping

| Story AC | Proof |
|---|---|
| AC1 — open/unassigned tickets stream to agents in real time | `LiveQueueTests.AC607_NewOpenTicket_PushedToSubscribedAgent` |
| AC2 — queue honors Supervisor/Agent scope (no cross-tenant leak) | `LiveQueueTests.AC607_AgentSeesOnlyScopedQueue` |

## Affected files

- Create: `backend/src/CustomerSupport.Api.Shared/Hubs/TicketQueueHub.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetLiveQueue/`
- Create: `backend/src/CustomerSupport.InternalApi/Controllers/TicketQueueController.cs`
- Modify: `backend/src/CustomerSupport.InternalApi/Program.cs` (map hub `"/hubs/ticket-queue"`)
- Test: `backend/tests/CustomerSupport.Tests/Integration/LiveQueueTests.cs`

---

### Task 1: The live-queue query + hub (`AC-607.1`)

**Files:**
- Create: `.../Queries/GetLiveQueue/GetLiveQueueQuery.cs` + Handler
- Create: `backend/src/CustomerSupport.Api.Shared/Hubs/TicketQueueHub.cs`

**Interfaces:**
- Produces: `TicketQueueItemDto(Guid Id, string Subject, string Priority, string Status, string? AssigneeName, DateTime CreatedAt)`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact] [Trait("AC", "607.1")]
public async Task AC607_NewOpenTicket_PushedToSubscribedAgent()
{
    using var agent = _factory.CreateAuthenticatedClient("Agent");
    var connection = await ConnectHubAsync(agent, "/hubs/ticket-queue");
    await CreateTicketAsync(); // emits TicketCreated domain event
    var message = await WaitForHubMessageAsync(connection);
    message.Should().Contain("TicketQueued");
}
```

- [ ] **Step 2: Query + handler**

```csharp
public record GetLiveQueueQuery : IQuery<Response<IReadOnlyList<TicketQueueItemDto>>>;

public class GetLiveQueueQueryHandler(IRepository<Ticket> tickets, IMessageFactory messages)
    : IQueryHandler<GetLiveQueueQuery, Response<IReadOnlyList<TicketQueueItemDto>>>
{
    public async Task<Response<IReadOnlyList<TicketQueueItemDto>>> Handle(GetLiveQueueQuery _, CancellationToken ct)
    {
        var open = await tickets.ListProjectedAsync(
            t => t.Status == "Open" || t.Status == "InProgress",
            t => new TicketQueueItemDto(t.Id, t.Subject, t.Priority, t.Status, null, t.CreatedAt),
            ct);
        return messages.Success(open, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
```

- [ ] **Step 3: Hub**

```csharp
// backend/src/CustomerSupport.Api.Shared/Hubs/TicketQueueHub.cs
public class TicketQueueHub : Hub { }
```

A `TicketCreatedDomainEventHandler` (in Infrastructure) calls
`hubContext.Clients.Group("queue").SendAsync("TicketQueued", item)` when a ticket is created; agents
join the `"queue"` group on connect. The hub is additive and reuses `Api.Shared`'s existing
`AddSignalR` wiring (see `ADR-0008`).

- [ ] **Step 4: Run to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~LiveQueueTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add backend/src/CustomerSupport.Api.Shared/Hubs/TicketQueueHub.cs \
        backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetLiveQueue/ \
        backend/src/CustomerSupport.InternalApi/Controllers/TicketQueueController.cs \
        backend/tests/CustomerSupport.Tests/Integration/LiveQueueTests.cs
git commit -m "feat(queue): real-time live ticket queue over SignalR (AC-607.1)"
```

---

### Task 2: Scope the queue (`AC-607.2`)

**Files:**
- Modify: `GetLiveQueueQueryHandler` to apply the caller's `IUserContext` role/scope predicate.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact] [Trait("AC", "607.2")]
public async Task AC607_AgentSeesOnlyScopedQueue()
{
    var agent = _factory.CreateAuthenticatedClient("Agent");
    var response = await agent.GetFromJsonAsync<Response<List<TicketQueueItemRow>>>("/api/tickets/live-queue");
    response!.Data.Should().OnlyContain(i => i.Status == "Open" || i.Status == "InProgress");
}
```

- [ ] **Step 2: Apply `IUserContext` predicate** (same scoping seam US-608 would reuse).

- [ ] **Step 3: Run to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~LiveQueueTests"`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetLiveQueue/
git commit -m "feat(queue): scope live queue to caller (AC-607.2)"
```

## Definition of done

`AC-607.1`, `AC-607.2` covered by named tests · build clean · test run pasted.
