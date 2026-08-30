# US-218 — Escalation Level Progression Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** A breached-and-unresolved ticket climbs from `Level1` to `Level2` to `Level3` as time passes,
using a configurable per-level threshold — the progression `FEAT-17`'s second slice explicitly left
unbuilt (spec A2: "no `Level2`/`Level3` progression logic").

**Architecture:** One new lookup entity (`EscalationLevel`, same shape as `SLAPolicy`/`Department`),
Admin-gated CRUD over it (mirrors `SLAPoliciesController`), and a progression check added to the
existing `SlaBreachScanner` pass — no new hosted service. `Ticket` gains `EscalatedAt` so the scanner
has something to measure "how long at this level" against.

**Spec:** `docs/superpowers/specs/EPIC-05-US-218-sla-escalation.md`, A2.

## Acceptance criteria (continuing from `US-217`'s `AC-231` — next free is `AC-232`)

AC-232. Given an active `EscalationLevel` config for `"Level2"` with `MinutesAfterBreach = N`, and a
ticket at `EscalationState = "Level1"` whose `EscalatedAt` is more than `N` minutes in the past, when
the scanner runs, then the ticket's `EscalationState` becomes `"Level2"` and `EscalatedAt` updates to
the current scan time.

AC-233. Given the same shape one level up (`"Level3"` config, a ticket at `"Level2"`), the progression
repeats — `"Level2"` → `"Level3"`.

AC-234. Given a ticket already at `"Level3"`, when the scanner runs, then nothing changes — `"Level3"`
is terminal, there is no `"Level4"`.

AC-235. Given no active `EscalationLevel` config exists for the ticket's next level, when the scanner
runs, then the ticket stays at its current level — an unconfigured level means "do not progress," not
an error.

AC-236. Given an Admin, when they create an `EscalationLevel` (`Level`, `MinutesAfterBreach`,
`TargetRole`), then it is stored and retrievable; a duplicate `Level` value is rejected `409`.

## Global Constraints

- `TargetRole` is stored on `EscalationLevel` now because `US-219` (notifications) needs it, but this
  plan does not use it — nothing here sends anything anywhere.
- New unique index (`EscalationLevel.Level`) gets the `IDbExceptionTranslator` pairing (the established
  `FEAT-16`/`CreateDepartmentCommandHandler` lesson) in `CreateEscalationLevelCommandHandler`.
- New failure code `ESCALATION_LEVEL_EXISTS` (409) registered in all three places
  (`SystemCode.cs`/`SystemCodeMap.cs`/`ResponseExtensions.MapFailureStatusCode`). This *is* a conflict, so
  it joins the `ERR002 or …` switch arm that already contains `ERR049`/`ERR050`/`ERR058`/`ERR059`.

---

### Task 1: `EscalationLevel` entity + admin CRUD (`AC-236`)

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Entities/Sla/EscalationLevel.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/EscalationLevelConfiguration.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Sla/Commands/CreateEscalationLevel/CreateEscalationLevelCommand.cs` (+ `Handler` + `Request` + `Validator`)
- Create: `backend/src/CustomerSupport.Application/Features/Sla/Queries/GetEscalationLevels/GetEscalationLevelsQuery.cs` (+ `Handler`)
- Create: `backend/src/CustomerSupport.InternalApi/Controllers/EscalationLevelsController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/EscalationLevelEndpointTests.cs`

**Interfaces:**
- Produces: `EscalationLevel(Guid Id, string Level, int MinutesAfterBreach, string TargetRole, bool IsActive)`.

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/CustomerSupport.Tests/Integration/EscalationLevelEndpointTests.cs (excerpt)
public class EscalationLevelEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;
    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_admin, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
    }
    public Task DisposeAsync() { _admin.Dispose(); return _factory.DisposeAsync().AsTask(); }

    private async Task<Guid> CreateLevelAsync(string level, int minutes, string role)
    {
        var r = await _admin.PostAsJsonAsync("/api/EscalationLevels", new
        {
            level, minutesAfterBreach = minutes, targetRole = role,
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await r.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    [Fact]
    [Trait("AC", "236")]
    public async Task AC236_CreateEscalationLevel_Returns201()
    {
        await CreateLevelAsync("Level2", 60, "Supervisor");
    }

    [Fact]
    [Trait("AC", "236")]
    public async Task AC236_CreateEscalationLevel_DuplicateLevel_Returns409()
    {
        await CreateLevelAsync("Level2", 60, "Supervisor");

        var response = await _admin.PostAsJsonAsync("/api/EscalationLevels", new
        {
            level = "Level2", minutesAfterBreach = 30, targetRole = "Admin",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~EscalationLevelEndpointTests"`
Expected: FAIL — route doesn't exist.

- [ ] **Step 3: Entity, config, command, controller**

```csharp
// backend/src/CustomerSupport.Domain/Entities/Sla/EscalationLevel.cs
namespace CustomerSupport.Domain.Entities.Sla;

/// <summary>US-218, AC-232..236 — a threshold for climbing one escalation level. Lookup-entity shape
/// like <see cref="SLAPolicy"/>; the unique <see cref="Level"/> is the conflict surface.</summary>
public class EscalationLevel : BaseEntity
{
    public string Level { get; private set; } = string.Empty;
    public int MinutesAfterBreach { get; private set; }
    public string TargetRole { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public static EscalationLevel Create(string level, int minutesAfterBreach, string targetRole)
    {
        if (level is not ("Level1" or "Level2" or "Level3"))
            throw new ArgumentException("Level must be Level1, Level2 or Level3", nameof(level));
        if (minutesAfterBreach <= 0)
            throw new ArgumentException("MinutesAfterBreach must be positive", nameof(minutesAfterBreach));
        if (string.IsNullOrWhiteSpace(targetRole))
            throw new ArgumentException("TargetRole is required", nameof(targetRole));

        return new EscalationLevel
        {
            Id = Guid.NewGuid(),
            Level = level.Trim(),
            MinutesAfterBreach = minutesAfterBreach,
            TargetRole = targetRole.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
```

```csharp
// backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/EscalationLevelConfiguration.cs
using CustomerSupport.Domain.Entities.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class EscalationLevelConfiguration : IEntityTypeConfiguration<EscalationLevel>
{
    public void Configure(EntityTypeBuilder<EscalationLevel> builder)
    {
        builder.ToTable("EscalationLevels");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Level).HasMaxLength(16).IsRequired();
        builder.Property(x => x.TargetRole).HasMaxLength(64).IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasIndex(x => x.Level).IsUnique().HasDatabaseName("IX_EscalationLevels_Level");
    }
}
```

`CreateEscalationLevelCommandHandler` — this **is** a real unique-index case, so it follows
`CreateDepartmentCommandHandler`'s exact shape (constructor includes `IDbExceptionTranslator`, the
`try { SaveChangesAsync } catch (Exception ex) when (dbExceptionTranslator.IsUniqueViolation(ex))`
block), substituting the error code:

```csharp
// backend/src/CustomerSupport.Application/Features/Sla/Commands/CreateEscalationLevel/CreateEscalationLevelCommandHandler.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Sla.Commands.CreateEscalationLevel;

public class CreateEscalationLevelCommandHandler(
    IRepository<EscalationLevel> levels,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IMessageFactory messages)
    : ICommandHandler<CreateEscalationLevelCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateEscalationLevelCommand request, CancellationToken ct)
    {
        var level = EscalationLevel.Create(request.Level, request.MinutesAfterBreach, request.TargetRole);

        await levels.AddAsync(level, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsUniqueViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.EscalationLevel.LEVEL_EXISTS, MessageType.Conflict);
        }

        return messages.Success(level.Id, ApplicationErrors.EscalationLevel.CREATED);
    }
}
```

`GetEscalationLevelsQueryHandler` follows `GetSLAPoliciesQueryHandler`'s shape (unpaged `ListAsync`,
project to a DTO) — matching the same "small, admin-managed lookup list" reasoning `SLAPolicies`' own
list endpoint already uses. `EscalationLevelsController` mirrors `SLAPoliciesController` route-for-route
(`[Authorize(Policy = "Authenticated")]` on the class, `[Authorize(Policy = "Admin")]` on the POST).

- [ ] **Step 4: Register the new codes**

`ApplicationErrors.cs`, add:
```csharp
/// <summary>US-218, AC-232..236.</summary>
public static class EscalationLevel
{
    public const string CREATED = "ESCALATION_LEVEL_CREATED";
    public const string LEVEL_EXISTS = "ESCALATION_LEVEL_EXISTS";
}
```
`SystemCode.cs`: add after `ERR060`
```csharp
public const string ERR061 = "ERR061"; // Escalation level already exists
```
and after `CON044`
```csharp
public const string CON047 = "CON047"; // Escalation level created
```
`SystemCodeMap.cs`: add
```csharp
["ESCALATION_LEVEL_CREATED"] = SystemCode.CON047,
["ESCALATION_LEVEL_EXISTS"] = SystemCode.ERR061,
```
`ResponseExtensions.MapFailureStatusCode`: add `SystemCode.ERR061` to the existing `ERR002 or … or
SystemCode.ERR059` 409 switch arm. `Resources.yaml`: add ar/en pairs for both keys.

- [ ] **Step 5: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~EscalationLevelEndpointTests"`
Expected: PASS, 2/2.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Entities/Sla/EscalationLevel.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/EscalationLevelConfiguration.cs \
        backend/src/CustomerSupport.Application/Features/Sla/Commands/CreateEscalationLevel/ \
        backend/src/CustomerSupport.Application/Features/Sla/Queries/GetEscalationLevels/ \
        backend/src/CustomerSupport.InternalApi/Controllers/EscalationLevelsController.cs \
        backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCode.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCodeMap.cs \
        backend/src/CustomerSupport.Api.Shared/Extensions/ResponseExtensions.cs \
        backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml \
        backend/tests/CustomerSupport.Tests/Integration/EscalationLevelEndpointTests.cs
git commit -m "feat(sla): EscalationLevel config entity + admin CRUD (US-218, AC-236)"
```

---

### Task 2: `Ticket.EscalatedAt` and progression in the scanner (`AC-232`–`AC-235`)

**Files:**
- Modify: `backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/Jobs/SlaBreachScanner.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/EscalationProgressionEndpointTests.cs`

**Interfaces:**
- Consumes: `EscalationLevel` (Task 1), `Ticket.Escalate(string)` (existing).
- Produces: `Ticket.EscalatedAt` (`DateTime?`).

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/CustomerSupport.Tests/Integration/EscalationProgressionEndpointTests.cs (excerpt)
// Helper ForceEscalationAsync mirrors SlaPauseAndEscalationEndpointTests: create a ticket, set one
// due date into the past via a scope+AppDbContext, run the scanner once (-> Level1), return the id.
public class EscalationProgressionEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;
    private Guid _categoryId, _customerId;
    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_admin, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        _categoryId = await _factory.EnsureCategoryAsync("Technical");
        var c = await _admin.PostAsJsonAsync("/api/Customers", new { name="Esc", email=$"esc-{Guid.NewGuid():N}@e.com", phone=(string?)null });
        _customerId = (await c.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }
    public Task DisposeAsync() { _admin.Dispose(); return _factory.DisposeAsync().AsTask(); }

    private async Task<Guid> CreateTicketAsync() { /* POST /api/Tickets, return id */ }

    private async Task ForceEscalationAsync(Guid ticketId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t = await db.Tickets.FirstAsync(x => x.Id == ticketId);
        db.Entry(t).Property(x => x.ResponseDueAt).CurrentValue = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>().ScanAsync();
    }

    [Fact]
    [Trait("AC", "232")]
    public async Task AC232_Level1TicketPastThreshold_ProgressesToLevel2()
    {
        await CreateLevelAsync("Level2", 0, "Supervisor"); // 0 = immediately eligible
        var ticketId = await CreateTicketAsync();
        await ForceEscalationAsync(ticketId);

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>().ScanAsync();

        var detail = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
        detail!.Data!.EscalationState.Should().Be("Level2");
    }

    [Fact]
    [Trait("AC", "234")]
    public async Task AC234_Level3Ticket_DoesNotProgressFurther()
    {
        await CreateLevelAsync("Level2", 0, "Supervisor");
        await CreateLevelAsync("Level3", 0, "Admin");
        var ticketId = await CreateTicketAsync();
        await ForceEscalationAsync(ticketId);
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>().ScanAsync();
            await scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>().ScanAsync();
        }

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>().ScanAsync();

        var detail = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
        detail!.Data!.EscalationState.Should().Be("Level3");
    }

    [Fact]
    [Trait("AC", "235")]
    public async Task AC235_NoConfigForNextLevel_TicketStaysPut()
    {
        var ticketId = await CreateTicketAsync();
        await ForceEscalationAsync(ticketId);

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>().ScanAsync();

        var detail = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
        detail!.Data!.EscalationState.Should().Be("Level1");
    }

    private async Task CreateLevelAsync(string level, int minutes, string role) { /* POST /api/EscalationLevels */ }
    public sealed record TicketRow(Guid Id, string Status, string RowVersion, string EscalationState);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~EscalationProgressionEndpointTests"`
Expected: FAIL — `EscalatedAt` doesn't exist, progression never happens.

- [ ] **Step 3: `Ticket.EscalatedAt`, set alongside every `Escalate()` call**

Edit the existing `Escalate(string level)` method in `Ticket.cs`:

```csharp
    public void Escalate(string level)
    {
        EscalationState = level;
        EscalatedAt = DateTime.UtcNow; // US-218, AC-232 — tracks "since when" for progression
        MarkUpdated();
    }
```

and add the field next to `EscalationState`:

```csharp
    /// <summary>US-218, AC-232 — when the current escalation level was set. Null until first
    /// escalation; the scanner measures MinutesAfterBreach against this, not against CreatedAt.</summary>
    public DateTime? EscalatedAt { get; private set; }
```

- [ ] **Step 4: Progression logic in the scanner**

Add this **after** the existing breach-recording `foreach` (and after US-217's warning pass, if that
plan has already been applied — all three live in the same `ScanAsync` and run in sequence):

```csharp
        // US-218, AC-232..235 — level progression for tickets already escalated. Level3 is terminal.
        var progressable = await db.Tickets
            .Where(t => (t.EscalationState == "Level1" || t.EscalationState == "Level2")
                && t.EscalatedAt != null)
            .ToListAsync(ct);

        if (progressable.Count > 0)
        {
            var nextLevelByCurrent = new Dictionary<string, string> { ["Level1"] = "Level2", ["Level2"] = "Level3" };

            var activeLevels = (await db.Set<EscalationLevel>().IgnoreQueryFilters()
                    .Where(l => l.IsActive)
                    .ToListAsync(ct))
                .ToDictionary(l => l.Level);

            var progressed = false;
            foreach (var ticket in progressable)
            {
                var nextLevel = nextLevelByCurrent[ticket.EscalationState];

                if (activeLevels.TryGetValue(nextLevel, out var config)
                    && now - ticket.EscalatedAt!.Value >= TimeSpan.FromMinutes(config.MinutesAfterBreach))
                {
                    ticket.Escalate(nextLevel); // also refreshes EscalatedAt (Step 3)
                    progressed = true;
                }
                // AC-235 — no config for the next level: leave the ticket at its current state.
            }

            if (progressed)
            {
                await db.SaveChangesAsync(ct);
            }
        }
```

`SlaBreachScanner` needs **no new constructor dependency** — `EscalationLevel` is read via the already-
injected `AppDbContext db`, the same way `SLAEvent` already is.

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~EscalationProgressionEndpointTests|FullyQualifiedName~SlaPauseAndEscalationEndpointTests|FullyQualifiedName~SlaWarningEndpointTests"`
Expected: PASS — new tests, plus no regression in the two SLA suites this scanner change sits beside.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs \
        backend/src/CustomerSupport.Infrastructure/Jobs/SlaBreachScanner.cs \
        backend/tests/CustomerSupport.Tests/Integration/EscalationProgressionEndpointTests.cs
git commit -m "feat(sla): escalation level progression (US-218, AC-232..235)"
```

## Definition of done

`AC-232` through `AC-236` each covered by a test naming it · full suite green · task record written to
`docs/superpowers/plans/EPIC-05-US-218-auto-escalation/README.md`. Notification-on-escalation is `US-219`'s
scope, not this plan's — `EscalationLevel.TargetRole` exists here only as the column that story will
read.
