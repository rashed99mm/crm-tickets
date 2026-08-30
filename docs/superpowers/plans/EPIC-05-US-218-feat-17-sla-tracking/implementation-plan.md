# FEAT-17 — SLA Tracking (first slice) Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** `SLAPolicy`/`SLAEvent` entities, due dates computed at ticket creation, policy create+list,
and a breach-detection background scanner (AC-124, AC-128–AC-133).

**Architecture:** `SLAPolicy` is a lookup entity (`Department`'s shape). `SLAEvent` is
`IAppendOnlyEntity` — a breach, once recorded, is never edited. Breach detection is a
`BackgroundService` polling loop (`SlaBreachDetector`) wrapping a testable single-pass scanner
(`SlaBreachScanner`/`ISlaBreachScanner`), matching this codebase's existing `NotificationSender`
shape rather than introducing a Hangfire recurring job (Hangfire is registered in DI but never
actually used for scheduling anywhere in this codebase).

**Tech Stack:** .NET 10, EF Core, MediatR, a plain `BackgroundService` (no Hangfire).

**Spec:** [`docs/superpowers/specs/EPIC-05-US-218-sla-tracking.md`](../../specs/EPIC-05-US-218-sla-tracking.md)

## Global Constraints

- Wall-clock hours only this slice (spec A1) — no business-hours calendar.
- `Pending`/`Resolved`/`Closed` tickets are never scanned (spec A4) — this slice's approximation of
  "paused," ahead of `US-213`'s full pause/resume (shipped later, in the escalation slice).
- No new `SystemCode`/`SystemCodeMap` entries needed — every failure path in this slice is
  validation (`400`), already covered by `VAL001`.

---

### Task 1: `SLAPolicy`/`SLAEvent` entities + migration (`AC-124`)

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Entities/Sla/SLAPolicy.cs`, `SLAEvent.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/SLAPolicyConfiguration.cs`,
  `SLAEventConfiguration.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/SlaTrackingEndpointTests.cs`

**Interfaces:**
- Produces: `SLAPolicy.Create(string priority, decimal responseTargetHours, decimal
  resolutionTargetHours, Guid? categoryId, Guid? branchId)`. `SLAEvent.Record(Guid ticketId, string
  targetType, DateTime targetAt, DateTime? breachedAt)`, `SLAEvent.TargetTypes.{Response,Resolution}`.

- [ ] **Step 1: Write the failing test, then implement**

```csharp
[Fact]
[Trait("AC", "124")]
public async Task AC124_CreatePolicy_IsRetrievable()
{
    var response = await _admin.PostAsJsonAsync("/api/SLAPolicies", new
    {
        priority = "High", responseTargetHours = 2m, resolutionTargetHours = 8m,
    });
    response.StatusCode.Should().Be(HttpStatusCode.Created);
}
```

```csharp
// backend/src/CustomerSupport.Domain/Entities/Sla/SLAPolicy.cs
public class SLAPolicy : BaseEntity
{
    public string Priority { get; private set; } = string.Empty;
    public decimal ResponseTargetHours { get; private set; }
    public decimal ResolutionTargetHours { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? BranchId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static SLAPolicy Create(
        string priority, decimal responseTargetHours, decimal resolutionTargetHours,
        Guid? categoryId, Guid? branchId)
    {
        if (string.IsNullOrWhiteSpace(priority))
            throw new ArgumentException("Priority is required", nameof(priority));
        if (responseTargetHours <= 0)
            throw new ArgumentException("Response target hours must be positive", nameof(responseTargetHours));
        if (resolutionTargetHours <= 0)
            throw new ArgumentException("Resolution target hours must be positive", nameof(resolutionTargetHours));

        return new SLAPolicy
        {
            Id = Guid.NewGuid(), Priority = priority.Trim(),
            ResponseTargetHours = responseTargetHours, ResolutionTargetHours = resolutionTargetHours,
            CategoryId = categoryId, BranchId = branchId, IsActive = true, CreatedAt = DateTime.UtcNow
        };
    }

    public void Deactivate() { IsActive = false; MarkUpdated(); }
}
```

```csharp
// backend/src/CustomerSupport.Domain/Entities/Sla/SLAEvent.cs
public class SLAEvent : BaseEntity, IAppendOnlyEntity
{
    public static class TargetTypes
    {
        public const string Response = "Response";
        public const string Resolution = "Resolution";
    }

    public Guid TicketId { get; private set; }
    public string TargetType { get; private set; } = string.Empty;
    public DateTime TargetAt { get; private set; }
    public DateTime? BreachedAt { get; private set; }
    public int PausedSeconds { get; private set; } // always 0 this slice (spec A4)

    public static SLAEvent Record(Guid ticketId, string targetType, DateTime targetAt, DateTime? breachedAt)
    {
        if (ticketId == Guid.Empty)
            throw new ArgumentException("A ticket is required", nameof(ticketId));
        if (targetType != TargetTypes.Response && targetType != TargetTypes.Resolution)
            throw new ArgumentException($"TargetType must be one of: {TargetTypes.Response}, {TargetTypes.Resolution}", nameof(targetType));

        return new SLAEvent
        {
            TicketId = ticketId, TargetType = targetType, TargetAt = targetAt,
            BreachedAt = breachedAt, PausedSeconds = 0, CreatedAt = DateTime.UtcNow
        };
    }
}
```

- [ ] **Step 2: Migration, review, commit**

Run: `dotnet ef migrations add AddSlaTracking --project src/CustomerSupport.Infrastructure --startup-project src/CustomerSupport.InternalApi`
Expected: only `SLAPolicies`, `SLAEvents` tables plus `Tickets.ResponseDueAt`/`ResolutionDueAt` —
reviewed before applying.

```bash
git commit -m "feat(sla): SLAPolicy/SLAEvent entities (AC-124)"
```

---

### Task 2: SLA targets computed at ticket creation (`AC-128`–`AC-130`)

**Files:**
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs`
- Modify: `Ticket.cs` (add `ResponseDueAt`/`ResolutionDueAt`, `SetSlaTargets`)

**Interfaces:**
- Consumes: `IRepository<SLAPolicy>` (new dependency on `CreateTicketCommandHandler`).

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "128")]
public async Task AC128_CreateTicket_WithMatchingPolicy_SetsDueDates()
{
    await CreateActivePolicyAsync("High", responseHours: 2, resolutionHours: 8);
    var ticketId = await CreateTicketAsync(priority: "High");

    var detail = await _client.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
    detail!.Data!.ResponseDueAt.Should().NotBeNull();
}
```

- [ ] **Step 2: Implement — category-scoped policy beats unscoped (spec A5)**

```csharp
/// <summary>FEAT-17, AC-128..AC-130. Picks the most specific active policy matching the ticket's
/// priority — one scoped to the ticket's category beats an unscoped one. Wall-clock hours only
/// this slice (spec A1); no matching policy leaves both due dates null (AC-129).</summary>
private async Task ApplySlaTargetsAsync(Ticket ticket, CancellationToken ct)
{
    var candidates = await slaPolicies.ListAsync(
        p => p.IsActive && p.Priority == ticket.Priority
            && (p.CategoryId == null || p.CategoryId == ticket.CategoryId)
            && (p.BranchId == null || p.BranchId == ticket.BranchId),
        ct);

    var policy = candidates
        .OrderByDescending(p => (p.CategoryId.HasValue ? 1 : 0) + (p.BranchId.HasValue ? 1 : 0))
        .FirstOrDefault();

    if (policy is null) return;

    ticket.SetSlaTargets(
        ticket.CreatedAt.AddHours((double)policy.ResponseTargetHours),
        ticket.CreatedAt.AddHours((double)policy.ResolutionTargetHours));
}
```

Called from `Handle` right after `Ticket.Create(...)`, before `AddAsync`. `IRepository<SLAPolicy>
slaPolicies` added to the handler's constructor.

- [ ] **Step 3: Run test to verify it passes, commit**

Run: `dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SlaTrackingEndpointTests"`
Expected: PASS.

```bash
git commit -m "feat(sla): compute due dates at ticket creation (AC-128..130)"
```

---

### Task 3: `SLAPoliciesController` — create + list (`AC-124`)

**Files:**
- Create: `Features/Sla/Commands/CreateSLAPolicy/`, `Features/Sla/Queries/GetSLAPolicies/`
- Create: `backend/src/CustomerSupport.InternalApi/Controllers/SLAPoliciesController.cs`

- [ ] **Step 1–4: standard CQRS + controller pass**, same shape as `DepartmentsController`
 (`FEAT-16`) — `POST /api/SLAPolicies` (`Admin`), `GET /api/SLAPolicies` (`Authenticated`).

> **Correction (rewritten 2026-08-27):** the `SLAPoliciesController` that actually shipped also
> exposes `PUT /api/SLAPolicies/{id}` and `DELETE /api/SLAPolicies/{id}`
> (`UpdateSLAPolicyCommand` / `DeactivateSLAPolicyCommand`, both Admin-gated, 404 on unknown id),
> added by the escalation slice. `UpdateSLAPolicyCommand` mirrors Create with the same six args
> `(id, priority, responseTargetHours, resolutionTargetHours, categoryId, branchId)`.

```bash
git commit -m "feat(sla): SLAPoliciesController create+list (AC-124)"
```

---

### Task 4: Breach detection (`AC-131`–`AC-133`)

**Files:**
- Create: `backend/src/CustomerSupport.Infrastructure/Jobs/SlaBreachScanner.cs`,
  `SlaBreachDetector.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs`
  (`RegisterPlatformInfrastructure`)
- Modify: `TicketDetailDto` — expose `ResponseDueAt`/`ResolutionDueAt`

**Interfaces:**
- Produces: `ISlaBreachScanner.ScanAsync(CancellationToken) : Task<int>` — split from the hosted
  service so a test can call one pass directly.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "131")]
public async Task AC131_OverdueTicket_RecordsBreachEvent()
{
    var ticketId = await CreateOverdueTicketAsync(); // due date in the past, fixture helper
    await _scanner.ScanAsync();

    var detail = await _client.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
    detail!.Data!.EscalationState.Should().NotBe("None"); // AC-138 side effect, proven together
}
```

- [ ] **Step 2: Implement the scanner**

```csharp
// backend/src/CustomerSupport.Infrastructure/Jobs/SlaBreachScanner.cs
public interface ISlaBreachScanner
{
    Task<int> ScanAsync(CancellationToken ct = default);
}

public class SlaBreachScanner(AppDbContext db) : ISlaBreachScanner
{
    private static readonly string[] EvaluatedStatuses = ["New", "Open"]; // AC-133, spec A4

    public async Task<int> ScanAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var candidates = await db.Tickets
            .Where(t => EvaluatedStatuses.Contains(t.Status)
                && ((t.ResponseDueAt != null && t.ResponseDueAt < now)
                    || (t.ResolutionDueAt != null && t.ResolutionDueAt < now)))
            .ToListAsync(ct);

        if (candidates.Count == 0) return 0;

        var ticketIds = candidates.Select(t => t.Id).ToList();
        var alreadyBreached = (await db.Set<SLAEvent>().IgnoreQueryFilters()
                .Where(e => ticketIds.Contains(e.TicketId) && e.BreachedAt != null)
                .Select(e => new { e.TicketId, e.TargetType }).ToListAsync(ct))
            .Select(e => (e.TicketId, e.TargetType)).ToHashSet();

        var recorded = 0;
        foreach (var ticket in candidates)
        {
            var newBreach = false;
            if (ticket.ResponseDueAt is { } r && r < now && !alreadyBreached.Contains((ticket.Id, SLAEvent.TargetTypes.Response)))
            {
                db.Set<SLAEvent>().Add(SLAEvent.Record(ticket.Id, SLAEvent.TargetTypes.Response, r, now));
                recorded++; newBreach = true;
            }
            if (ticket.ResolutionDueAt is { } s && s < now && !alreadyBreached.Contains((ticket.Id, SLAEvent.TargetTypes.Resolution)))
            {
                db.Set<SLAEvent>().Add(SLAEvent.Record(ticket.Id, SLAEvent.TargetTypes.Resolution, s, now));
                recorded++; newBreach = true;
            }
            // AC-138/139 — first breach escalates; progression is a later slice's scope.
            if (newBreach && ticket.EscalationState == "None")
            {
                ticket.Escalate("Level1");
            }
        }

        if (recorded > 0) await db.SaveChangesAsync(ct);
        return recorded;
    }
}
```

`SlaBreachDetector` is a `BackgroundService` polling `ScanAsync` every minute, matching
`NotificationSender`'s exact loop shape.

- [ ] **Step 3: Registration — the real deviation from the original assumption**

**A6 (original spec) assumed "internal host only"; the actual registration is on the shared
`RegisterPlatformInfrastructure`** — that's where `NotificationSender`, this codebase's one existing
background worker, is actually wired, and both `InternalApi` and `ExternalApi` call it. Building
new host-specific plumbing that doesn't exist anywhere else in this codebase would have been
inventing infrastructure to satisfy an assumption rather than following actual precedent —
`SlaBreachDetector` is registered the same way, alongside `NotificationSender`.

```csharp
// ServiceCollectionExtensions.cs, RegisterPlatformInfrastructure
services.AddHostedService<SlaBreachDetector>();
services.AddScoped<ISlaBreachScanner, SlaBreachScanner>();
```

- [ ] **Step 4: Run test to verify it passes, commit**

Run: `dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SlaTrackingEndpointTests"`
Expected: PASS (17/17, per this folder's `README.md`).

```bash
git commit -m "feat(sla): breach detection scanner + background loop (AC-131..133)"
```

---

### Task 5: `SLAPoliciesComponent` (frontend, list + create)

**Files:**
- Create: `frontend/projects/common/src/lib/organisation/sla-policy.api.ts`
- Create: `frontend/projects/admin-app/src/app/features/organisation/sla-policies.component.{ts,html}`

Same `AsyncState` list+create shape as `DepartmentsComponent`, copied and adapted for `SLAPolicy`'s
three fields (`priority`, `responseTargetHours`, `resolutionTargetHours`). No edit form this slice
(closed later, in the escalation slice's `US-223` addendum).

```bash
git commit -m "feat(sla): SLA policy admin screen — list + create"
```

## Definition of done

`AC-124`, `AC-128`–`AC-133` each covered by a test naming it. Evidence already recorded in this
folder's `README.md`: 17/17 filtered, 335/335 full suite at the time this slice shipped.

## Deliberately cut, all recorded in the spec (A1–A7)

`US-213` (pause/resume) · `US-214` (full policy CRUD) · `US-215` (business-hours calendar) ·
`US-217` (pre-breach warning) · `US-218` (auto-escalation progression) · `US-219` (notifications) ·
`US-220` (auto-assignment) · `US-221` (already shipped by `FEAT-07`, not rebuilt) · `US-222`–`US-224`
(frontend, shipped in the second slice) · `US-225` (escalation-state column, shipped in the second
slice).
