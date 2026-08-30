# US-219 — SLA Breach & Escalation Notifications Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** When a ticket breaches or escalates, create a `Notification` row for every user holding the
escalation level's `TargetRole` — the notification half of `FEAT-17`'s second slice (spec A2: "the user
asked explicitly to skip notifications this pass").

**Architecture:** The scanner (already touched by `US-217`/`US-218`) gains one more step: on a new
breach or a level progression, call `IIdentityUserService.GetUsersInRoleAsync` (already exists) and
create one `Notification.Create(...)` per user, reusing the inherited platform's own entity and write
path — no new notification infrastructure.

**A load-bearing limitation, stated up front, not discovered later:** `NotificationSender`
(`backend/src/CustomerSupport.Infrastructure/Jobs/NotificationSender.cs`), the platform's one existing
delivery worker, has a `SendNotificationAsync` method that is a literal `return Task.CompletedTask;` —
it marks every notification `"Sent"` without actually delivering it anywhere (no email, no push, no
in-app socket push). This plan creates real `Notification` rows with real, correct content and
targeting; it does **not** fix that pre-existing no-op, which is a platform-wide gap affecting every
notification this codebase has ever created, not something `US-219` introduced or is scoped to fix. A
user watching only the `Notifications` list/read-state API will see it work end to end; a user
expecting an actual email or push will not, today, for any feature.

**Spec:** `docs/superpowers/specs/EPIC-05-US-218-sla-escalation.md`, A2.

## Acceptance criteria (continuing from `US-218`'s `AC-236` — next free is `AC-237`)

AC-237. Given a ticket's escalation state changes (a new breach setting it to `"Level1"`, or a
progression to `"Level2"`/`"Level3"`), when the scanner records that change, then one `Notification`
is created per active user holding the matching `EscalationLevel.TargetRole`, with
`NotificationType = "SlaEscalation"` and a `Message` naming the ticket reference and new level.

AC-238. Given no active `EscalationLevel` row exists for a level (e.g. `"Level1"`, which `US-218`'s
config table only names for `Level2`/`Level3` progression, not the first breach), then no notification
is created for that transition — silently, not as an error, since `"Level1"` has no configured target
role by design.

AC-239. Given the same ticket transition is processed twice (defended against by the scanner's own
duplicate-breach guard), then notifications are not duplicated — this rides on `US-218`'s progression
only firing once per threshold crossing, not a new guard of its own.

## Global Constraints

- No new entity — `Notification`
  (`CustomerSupport.Domain/Entities/Notifications/Notification.cs`) already exists and is reused exactly
  as `NotificationSender` and any other creator of it uses it. Its `Create` signature is
  `Create(Guid userId, string title, string message, string notificationType, string channel, string? metadata = null)`.
- `IIdentityUserService.GetUsersInRoleAsync(string role, CancellationToken ct)` already exists and
  returns `IReadOnlyList<ApplicationUser>` (each with `.Id`). No new port.
- This plan does **not** touch `NotificationSender.SendNotificationAsync` — see the limitation note
  above. Fixing that no-op is real, separate scope (it would affect every existing notification type,
  not just SLA ones) and is not silently bundled into this story.

---

### Task 1: Notify on escalation transitions (`AC-237`–`AC-239`)

**Files:**
- Modify: `backend/src/CustomerSupport.Infrastructure/Jobs/SlaBreachScanner.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/SlaEscalationNotificationEndpointTests.cs`

**Interfaces:**
- Consumes: `IIdentityUserService.GetUsersInRoleAsync` (existing), `Notification.Create` (existing),
  `EscalationLevel` (`US-218`).
- Produces: nothing new — this task only adds a side effect to the existing scanner passes.

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/CustomerSupport.Tests/Integration/SlaEscalationNotificationEndpointTests.cs (excerpt)
public class SlaEscalationNotificationEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;
    private HttpClient _supervisor = null!;
    private Guid _categoryId, _customerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_admin, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        (_supervisor, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Supervisor);
        _categoryId = await _factory.EnsureCategoryAsync("Technical");
        var c = await _admin.PostAsJsonAsync("/api/Customers", new { name="Notif", email=$"n-{Guid.NewGuid():N}@e.com", phone=(string?)null });
        _customerId = (await c.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }
    public Task DisposeAsync() { _admin.Dispose(); _supervisor.Dispose(); return _factory.DisposeAsync().AsTask(); }

    private async Task<Guid> CreateTicketAsync() { /* POST /api/Tickets */ }
    private async Task<Guid> CreateLevelAsync(string l, int m, string r) { /* POST /api/EscalationLevels */ }

    [Fact]
    [Trait("AC", "237")]
    public async Task AC237_Level2Progression_NotifiesTargetRoleUsers()
    {
        await CreateLevelAsync("Level2", 0, "Supervisor");
        var ticketId = await CreateTicketAsync();
        // Force Level1 first (mirror ForceEscalationAsync from US-218 test), then run the scanner to progress.
        await ForceLevel1Async(ticketId);
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>().ScanAsync();

        var notes = await _supervisor.GetFromJsonAsync<Response<PaginatedList<NotificationRow>>>(
            "/api/Notifications?pageSize=50");
        notes!.Data!.Items.Should().Contain(n =>
            n.NotificationType == "SlaEscalation" && n.Message.Contains(ticketId.ToString().AsSpan(0, 8).ToString()));
    }

    [Fact]
    [Trait("AC", "238")]
    public async Task AC238_FirstBreachToLevel1_NoConfiguredRole_CreatesNoNotification()
    {
        // No EscalationLevel row for "Level1" — US-218's config only covers progression targets.
        var ticketId = await CreateTicketAsync();
        await ForceLevel1Async(ticketId);

        var before = await CountSupervisorNotificationsAsync();
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>().ScanAsync();
        var after = await CountSupervisorNotificationsAsync();

        after.Should().Be(before);
    }

    private async Task<int> CountSupervisorNotificationsAsync()
    {
        var notes = await _supervisor.GetFromJsonAsync<Response<PaginatedList<NotificationRow>>>(
            "/api/Notifications?pageSize=50");
        return notes!.Data!.TotalCount;
    }
    private sealed record NotificationRow(Guid Id, string NotificationType, string Message, string Title);
}
```

(Helpers `CreateTicketAsync`, `CreateLevelAsync`, `ForceLevel1Async` follow the same shape as the
US-218 test file — set `ResponseDueAt` to the past via a scope, run the scanner once.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SlaEscalationNotificationEndpointTests"`
Expected: FAIL — no notifications are created today regardless of escalation.

- [ ] **Step 3: Wire notification creation into the scanner's escalation paths**

`SlaBreachScanner`'s constructor gains `IIdentityUserService identityUsers` (add it as a second
parameter: `public class SlaBreachScanner(AppDbContext db, IIdentityUserService identityUsers)` — the
existing single-parameter primary constructor becomes `(AppDbContext db, IIdentityUserService identityUsers)`,
matching the C# primary-constructor style already used throughout this file). A private helper,
called from both the first-breach path (`US-218`'s `ticket.Escalate("Level1")` call site — note the
existing breach loop currently calls `ticket.Escalate("Level1")` directly inside `if (newBreach && ticket.EscalationState == "None")`) and the `US-218` progression loop (`ticket.Escalate(nextLevel)`):

```csharp
    private async Task NotifyEscalationAsync(Ticket ticket, string newLevel, CancellationToken ct)
    {
        var config = (await db.Set<EscalationLevel>().IgnoreQueryFilters()
                .Where(l => l.IsActive && l.Level == newLevel)
                .ToListAsync(ct))
            .SingleOrDefault();

        if (config is null)
        {
            return; // AC-238 — no configured target role for this level, nothing to notify.
        }

        var targets = await identityUsers.GetUsersInRoleAsync(config.TargetRole, ct);

        foreach (var user in targets)
        {
            db.Set<Notification>().Add(Notification.Create(
                user.Id,
                title: $"Ticket {ticket.Reference} escalated",
                message: $"Ticket {ticket.Reference} reached {newLevel}.",
                notificationType: "SlaEscalation",
                channel: "InApp"));
        }
    }
```

Call sites — both already sit inside loops with access to `ct`, and `ScanAsync` is already
`async Task<int>`:

- In the existing breach loop, change
  ```csharp
  if (newBreach && ticket.EscalationState == "None")
  {
      ticket.Escalate("Level1");
  }
  ```
  to
  ```csharp
  if (newBreach && ticket.EscalationState == "None")
  {
      ticket.Escalate("Level1");
      await NotifyEscalationAsync(ticket, "Level1", ct);
  }
  ```
- In `US-218`'s progression loop, change `ticket.Escalate(nextLevel);` to
  ```csharp
  ticket.Escalate(nextLevel);
  await NotifyEscalationAsync(ticket, nextLevel, ct);
  ```

Both edits append `Notification` rows to the same `db` context the surrounding pass already saves via
its `SaveChangesAsync`, so no extra flush is needed. (`db.Set<Notification>()` is correct even though
`AppDbContext` exposes no `DbSet<Notification>` property — `Set<T>()` works for any mapped entity.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SlaEscalationNotificationEndpointTests|FullyQualifiedName~EscalationProgressionEndpointTests|FullyQualifiedName~SlaPauseAndEscalationEndpointTests"`
Expected: PASS — new tests plus no regression in the two SLA suites this same file now also touches.

- [ ] **Step 5: Commit**

```bash
git add backend/src/CustomerSupport.Infrastructure/Jobs/SlaBreachScanner.cs \
        backend/tests/CustomerSupport.Tests/Integration/SlaEscalationNotificationEndpointTests.cs
git commit -m "feat(sla): notify target-role users on escalation (US-219, AC-237..239)"
```

## Definition of done

`AC-237` through `AC-239` each covered by a test naming it · full suite green · task record written to
`docs/superpowers/plans/EPIC-05-US-219-sla-notifications/README.md`, **explicitly repeating the
`NotificationSender` no-op limitation** so it is not lost between this plan and whoever executes it.
