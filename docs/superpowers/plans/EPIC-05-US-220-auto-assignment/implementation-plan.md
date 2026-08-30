# US-220 — Round-Robin Auto-Assignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** An unassigned `New` ticket gets automatically handed to the next Agent in rotation, rather than
sitting in the queue until a supervisor manually assigns it — the capability `FEAT-17`'s spec explicitly
called "a genuinely separate capability (round-robin/load-based strategy config, new tables) unrelated
to the breach/escalation loop" and left uncut for a later pass.

**Architecture:** Round-robin only — no load-based strategy (spec's own phrasing names both as options;
round-robin is the smaller, deterministic one and nothing in `US-220`'s acceptance criteria requires
load-awareness). One new single-row cursor table, one new `BackgroundService` following the exact
`SlaBreachDetector`/`NotificationSender` shape already established in this codebase, reusing
`AssignTicketCommand`'s domain call (`Ticket.AssignTo`) rather than duplicating assignment logic.

**Spec:** `docs/superpowers/specs/EPIC-05-US-218-sla-tracking.md`, A2 ("no auto-escalation, no
auto-assignment… this slice"), and `docs/superpowers/specs/EPIC-05-US-218-sla-escalation.md`, A3
(repeats the cut, names it "a genuinely separate capability").

## Acceptance criteria (continuing from `US-219`'s `AC-239` — next free is `AC-240`)

AC-240. Given one or more active users hold the `Agent` role, and a ticket exists with `Status = "New"`
and `AssigneeId = null`, when the auto-assignment pass runs, then the ticket is assigned to the next
agent in rotation (the one after whichever agent was assigned last, wrapping around).

AC-241. Given the rotation has assigned agent A most recently, when two more unassigned tickets exist
and the pass runs, then they go to agent B and agent C respectively (or back to A if there are only two
active agents) — each pass advances the cursor by exactly one ticket, never assigning two tickets to the
same agent in the same pass before every other active agent has had a turn.

AC-242. Given no active `Agent`-role users exist, when the pass runs, then unassigned tickets are left
untouched and no error is raised — there is nobody to assign to, which is a normal state, not a failure.

AC-243. Given a ticket already has an assignee, the pass never touches it — auto-assignment only ever
fills a `null` `AssigneeId`, it does not reassign.

## Global Constraints

- Assignment goes through the existing `Ticket.AssignTo(assigneeId, actorId)` domain method — the same
  method `AssignTicketCommandHandler` calls — so `TicketHistory` records the assignment exactly as a
  manual one would (an assignment's audit trail should not distinguish "a supervisor did this" from "the
  system did this" by being incomplete for one of them). `actorId` for an auto-assignment is the
  well-known `SystemActor.Id` (see below) — `TicketHistory` stores the GUID and no FK to `AspNetUsers` is
  enforced, so a constant is safe; if the product later wants the system actor to be a real, resolvable
  user, a seeded "system" account should be added (out of this story's scope).
- No new error codes — this is a background pass with no caller-facing failure path.

**Design assumption surfaced (not silently chosen):** this codebase has no existing "system actor" GUID
convention. This plan introduces `CustomerSupport.Domain/Common/SystemActor.cs` with a single fixed
`public static Guid Id => Guid.Parse("11111111-1111-1111-1111-111111111111")`. If an executor finds a
pre-existing convention (e.g. a seeded service account), they should reuse that instead and delete the
new file — the rest of the plan references `SystemActor.Id` and is unaffected by which constant backs it.

---

### Task 1: `SystemActor` + round-robin cursor + assignment scanner

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Common/SystemActor.cs`
- Create: `backend/src/CustomerSupport.Domain/Entities/Tickets/AutoAssignmentCursor.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/AutoAssignmentCursorConfiguration.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Jobs/AutoAssignmentScanner.cs` (interface
  `IAutoAssignmentScanner` + `BackgroundService` `AutoAssignmentDetector`)
- Modify: `backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/AutoAssignmentEndpointTests.cs`

**Interfaces:**
- Produces: `IAutoAssignmentScanner.ScanAsync(CancellationToken ct) : Task<int>` (number of tickets
  assigned this pass, matching `ISlaBreachScanner`'s own return-a-count shape for the same
  test-friendliness reason).

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/CustomerSupport.Tests/Integration/AutoAssignmentEndpointTests.cs (excerpt)
public class AutoAssignmentEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;
    private Guid _categoryId, _customerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_admin, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        _categoryId = await _factory.EnsureCategoryAsync("Technical");
        var c = await _admin.PostAsJsonAsync("/api/Customers", new { name="AA", email=$"aa-{Guid.NewGuid():N}@e.com", phone=(string?)null });
        _customerId = (await c.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }
    public Task DisposeAsync() { _admin.Dispose(); return _factory.DisposeAsync().AsTask(); }

    private async Task<Guid> CreateUnassignedTicketAsync()
    {
        var r = await _admin.PostAsJsonAsync("/api/Tickets", new
        {
            subject = $"AA {Guid.NewGuid():N}", description = "x",
            customerId = _customerId, categoryId = _categoryId, priority = "Normal",
        });
        return (await r.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    [Fact]
    [Trait("AC", "240")]
    public async Task AC240_UnassignedNewTicket_GetsAssignedToAnAgent()
    {
        var (_, agent) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
        var ticketId = await CreateUnassignedTicketAsync();

        var scanner = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IAutoAssignmentScanner>();
        var assigned = await scanner.ScanAsync(CancellationToken.None);

        assigned.Should().BeGreaterThanOrEqualTo(1);
        var detail = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
        detail!.Data!.AssigneeId.Should().Be(agent.Id);
    }

    [Fact]
    [Trait("AC", "241")]
    public async Task AC241_TwoUnassignedTickets_TwoDistinctAgents()
    {
        var (_, agentA) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
        var (_, agentB) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
        var first = await CreateUnassignedTicketAsync();
        var second = await CreateUnassignedTicketAsync();

        var scanner = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IAutoAssignmentScanner>();
        await scanner.ScanAsync(CancellationToken.None);

        var f = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{first}");
        var s = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{second}");
        f!.Data!.AssigneeId.Should().NotBe(s!.Data!.AssigneeId);
    }

    [Fact]
    [Trait("AC", "243")]
    public async Task AC243_AlreadyAssignedTicket_IsUntouched()
    {
        var (_, agent) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
        var ticketId = await CreateUnassignedTicketAsync();
        await _admin.PostAsJsonAsync($"/api/Tickets/{ticketId}/assign",
            new { assigneeId = agent.Id, rowVersion = await RowVersionAsync(ticketId) });

        var before = (await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}"))!.Data!.AssigneeId;
        var scanner = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IAutoAssignmentScanner>();
        await scanner.ScanAsync(CancellationToken.None);
        var after = (await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}"))!.Data!.AssigneeId;

        after.Should().Be(before);
    }

    private async Task<string> RowVersionAsync(Guid id)
    {
        var t = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{id}");
        return t!.Data!.RowVersion;
    }
    public sealed record TicketRow(Guid Id, string Status, string RowVersion, string EscalationState, Guid? AssigneeId);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AutoAssignmentEndpointTests"`
Expected: FAIL — `IAutoAssignmentScanner` doesn't exist.

- [ ] **Step 3: Cursor entity + `SystemActor` + config**

```csharp
// backend/src/CustomerSupport.Domain/Common/SystemActor.cs
namespace CustomerSupport.Domain.Common;

/// <summary>US-220, AC-240 — the actor recorded on system-performed assignments. A fixed well-known
/// GUID so TicketHistory can attribute auto-assignments without a persisted "system" user row. If the
/// product introduces a real service account, point this at that id and delete the literal.</summary>
public static class SystemActor
{
    public static Guid Id => Guid.Parse("11111111-1111-1111-1111-111111111111");
}
```

```csharp
// backend/src/CustomerSupport.Domain/Entities/Tickets/AutoAssignmentCursor.cs
namespace CustomerSupport.Domain.Entities.Tickets;

/// <summary>US-220, AC-240..243 — single-row rotation state for the whole platform this pass
/// (per-department/category rotation is real follow-on scope this story's ACs don't ask for).</summary>
public class AutoAssignmentCursor : BaseEntity
{
    public Guid? LastAssignedAgentId { get; private set; }

    public static AutoAssignmentCursor CreateEmpty() => new() { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };

    public void Advance(Guid agentId)
    {
        LastAssignedAgentId = agentId;
        MarkUpdated();
    }
}
```

```csharp
// backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/AutoAssignmentCursorConfiguration.cs
using CustomerSupport.Domain.Entities.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class AutoAssignmentCursorConfiguration : IEntityTypeConfiguration<AutoAssignmentCursor>
{
    public void Configure(EntityTypeBuilder<AutoAssignmentCursor> builder)
    {
        builder.ToTable("AutoAssignmentCursors");
        builder.HasKey(x => x.Id);
    }
}
```

- [ ] **Step 4: The scanner**

```csharp
// backend/src/CustomerSupport.Infrastructure/Jobs/AutoAssignmentScanner.cs
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Jobs;

public interface IAutoAssignmentScanner
{
    Task<int> ScanAsync(CancellationToken ct = default);
}

/// <summary>US-220, AC-240..243 — round-robin assignment of unassigned New tickets. Shares
/// SlaBreachScanner's split shape (a plain class the hosted service loops, testable without the
/// BackgroundService's own timer) for the same reason.</summary>
public class AutoAssignmentScanner(AppDbContext db, IIdentityUserService identityUsers) : IAutoAssignmentScanner
{
    public async Task<int> ScanAsync(CancellationToken ct = default)
    {
        var unassigned = await db.Tickets
            .Where(t => t.Status == "New" && t.AssigneeId == null)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        if (unassigned.Count == 0)
            return 0;

        // Reuse the existing port rather than reaching for UserManager directly — matches every other
        // caller in this codebase (e.g. AssignTicketCommandHandler).
        var agents = (await identityUsers.GetUsersInRoleAsync(ApplicationRole.Roles.Agent, ct))
            .Where(a => a.IsActive)
            .OrderBy(a => a.Id) // stable, arbitrary-but-consistent rotation order
            .ToList();

        if (agents.Count == 0)
            return 0; // AC-242 — nobody to assign to.

        var cursor = await db.Set<AutoAssignmentCursor>().IgnoreQueryFilters().FirstOrDefaultAsync(ct)
                   ?? AutoAssignmentCursor.CreateEmpty();
        if (cursor.Id == Guid.Empty)
            db.Set<AutoAssignmentCursor>().Add(cursor);

        var startIndex = cursor.LastAssignedAgentId is { } lastId
            ? (agents.FindIndex(a => a.Id == lastId) + 1) % agents.Count
            : 0;

        var assigned = 0;
        foreach (var ticket in unassigned)
        {
            var nextAgent = agents[(startIndex + assigned) % agents.Count];
            ticket.AssignTo(nextAgent.Id, SystemActor.Id);
            cursor.Advance(nextAgent.Id);
            assigned++;
        }

        await db.SaveChangesAsync(ct);
        return assigned;
    }
}

public class AutoAssignmentDetector(IServiceProvider services, ILogger<AutoAssignmentDetector> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1); // matches NotificationSender/SlaBreachDetector

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var scanner = scope.ServiceProvider.GetRequiredService<IAutoAssignmentScanner>();
                await scanner.ScanAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Auto-assignment pass failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
```

- [ ] **Step 5: Register the hosted service**

In `ServiceCollectionExtensions.RegisterPlatformInfrastructure`, alongside the existing
`services.AddScoped<ISlaBreachScanner, SlaBreachScanner>(); services.AddHostedService<SlaBreachDetector>();`:

```csharp
        services.AddScoped<IAutoAssignmentScanner, AutoAssignmentScanner>();
        services.AddHostedService<AutoAssignmentDetector>();
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AutoAssignmentEndpointTests"`
Expected: PASS, 3/3.

- [ ] **Step 7: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Common/SystemActor.cs \
        backend/src/CustomerSupport.Domain/Entities/Tickets/AutoAssignmentCursor.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/AutoAssignmentCursorConfiguration.cs \
        backend/src/CustomerSupport.Infrastructure/Jobs/AutoAssignmentScanner.cs \
        backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs \
        backend/tests/CustomerSupport.Tests/Integration/AutoAssignmentEndpointTests.cs
git commit -m "feat(tickets): round-robin auto-assignment (US-220, AC-240..243)"
```

## Definition of done

`AC-240` through `AC-243` each covered by a test naming it · full suite green, no regression in
`AssignTicketCommand`'s own manual-assign tests · task record written to
`docs/superpowers/plans/EPIC-05-US-220-auto-assignment/README.md`. Per-department/category rotation,
load-based strategy, and a supervisor-facing on/off toggle are named as real, deliberately-cut follow-on
scope, not silently assumed away.
