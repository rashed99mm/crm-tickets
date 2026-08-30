# US-217 — SLA Pre-Breach Warning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Set `Ticket.EscalationState = "Warning"` when a ticket is approaching (not yet past) its SLA
due date, so a supervisor scanning the queue can see trouble coming rather than only after it has
already happened.

**Architecture:** One new `Ticket.Warn()` domain method (mirrors the existing `Escalate()` exactly)
and one new check inside the existing `SlaBreachScanner.ScanAsync` pass — no new hosted service, no
new table. Reuses the 20%-of-window threshold already established server-independently for the
frontend countdown (`FEAT-17` 2026-08-27 addendum, `AC-156`), computed here in C# instead of
TypeScript so both halves agree on what "approaching" means.

**Spec:** `docs/superpowers/specs/EPIC-05-US-218-sla-tracking.md` (A3: "no pre-breach warning…
cut with `US-217`") and `docs/superpowers/specs/EPIC-05-US-218-sla-escalation.md` (A2:
`"Warning"` exists as an enum value per `BR-32` but nothing ever sets it — this plan is what sets it).

## Acceptance criteria (continuing from `US-215`'s `AC-228` — next free is `AC-229`)

AC-229. Given a ticket in `New`/`Open` status with `EscalationState = "None"` and a due date
(`ResponseDueAt` or `ResolutionDueAt`) that has not yet passed but is within 20% of the window
between `CreatedAt` and that due date, when the breach scanner runs, then `EscalationState` becomes
`"Warning"`.

AC-230. Given a ticket already at `EscalationState` `"Warning"`, `"Level1"` or beyond, when the
scanner runs again, then a still-pending (not yet breached) due date does not change its state —
`Warn()` only ever moves a ticket out of `"None"`, never overwrites an existing non-`"None"` value.

AC-231. Given a ticket with no due dates set (no matching `SLAPolicy` at creation), then it is never
warned — `Warn()` is only evaluated for tickets the scanner already loads (which requires a non-null
due date by construction of the existing query).

## Global Constraints

- `Ticket.Warn()` follows `Escalate()`'s exact division of responsibility: the method sets the state
  unconditionally, and the caller (`SlaBreachScanner`) is responsible for only calling it when
  `EscalationState == "None"` — matching this codebase's own documented reasoning for why `Escalate()`
  is unguarded (see `Ticket.Escalate`'s XML comment in `Ticket.cs`).
- No new error codes, no new endpoint — this is scanner-only, observable through the existing
  `TicketDetailDto.EscalationState` / `TicketListItemDto.EscalationState` fields already shipped.
- New success/SystemCode entries are **not** required — the scanner writes no user-facing envelope.

---

### Task 1: `Ticket.Warn()` and the scanner's warning check

**Files:**
- Modify: `backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/Jobs/SlaBreachScanner.cs`
- Test: `backend/tests/CustomerSupport.Tests/Unit/Domain/TicketSlaWarningTests.cs`,
  `backend/tests/CustomerSupport.Tests/Integration/SlaWarningEndpointTests.cs`

**Interfaces:**
- Produces: `Ticket.Warn()` — `void`, no parameters (unlike `Escalate(string level)`, there is only
  one warning state, so nothing to parameterise).
- Consumes: `Ticket.CreatedAt`, `Ticket.ResponseDueAt`/`ResolutionDueAt` (existing),
  `SlaBreachScanner`'s existing `EvaluatedStatuses` and `now` variable.

- [ ] **Step 1: Write the failing unit test**

```csharp
// backend/tests/CustomerSupport.Tests/Unit/Domain/TicketSlaWarningTests.cs
using CustomerSupport.Domain.Entities.Tickets;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

public class TicketSlaWarningTests
{
    private static Ticket NewTicket()
        => Ticket.Create(
            reference: "TKT-WARN-1",
            subject: "Warning test",
            description: "Body",
            customerId: Guid.NewGuid(),
            categoryId: Guid.NewGuid(),
            priority: "Normal",
            actorId: Guid.NewGuid());

    [Fact]
    [Trait("AC", "229")]
    public void Warn_FromNone_SetsWarning()
    {
        var ticket = NewTicket();

        ticket.Warn();

        ticket.EscalationState.Should().Be("Warning");
    }

    [Fact]
    [Trait("AC", "230")]
    public void Warn_CalledTwice_StaysWarning()
    {
        var ticket = NewTicket();
        ticket.Warn();

        ticket.Warn();

        ticket.EscalationState.Should().Be("Warning");
    }
}
```

(`Ticket.Create`'s real signature is
`Create(reference, subject, description, customerId, categoryId, priority, actorId)` — copied from the
actual method in `Ticket.cs`, not retyped from memory.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketSlaWarningTests"`
Expected: FAIL — `Warn` does not exist on `Ticket`.

- [ ] **Step 3: Add `Ticket.Warn()`**

In `Ticket.cs`, immediately after the existing `Escalate(string level)` method (which currently is):

```csharp
    public void Escalate(string level)
    {
        EscalationState = level;
        MarkUpdated();
    }
```

add:

```csharp
    /// <summary>
    /// Raises the escalation state to "Warning" (US-217, AC-229). Only ever called by the breach
    /// scanner when EscalationState is still "None" (AC-230) — same caller-owns-the-guard shape as
    /// Escalate(), for the same reason: this method has no way to distinguish "already past warning,
    /// leave it" from a future slice legitimately wanting to force the state back down.
    /// </summary>
    public void Warn()
    {
        EscalationState = "Warning";
        MarkUpdated();
    }
```

- [ ] **Step 4: Widen the scanner to also warn approaching tickets**

`SlaBreachScanner.ScanAsync` currently only loads tickets whose due date has *already* passed (its
`candidates` query). Add a second, separate query for tickets approaching their due date, and warn
them in the same pass. Insert this **after** the existing `candidates`/`alreadyBreached` block and
before the `foreach` breach loop (so both run in one pass and one `SaveChangesAsync`):

```csharp
        // US-217, AC-229..231 — tickets not yet breached but within 20% of their window, matching
        // the same threshold the frontend countdown already uses independently (FEAT-17 addendum,
        // AC-156) so both halves of the product agree on what "approaching" means.
        var approaching = await db.Tickets
            .Where(t => EvaluatedStatuses.Contains(t.Status)
                && t.EscalationState == "None"
                && ((t.ResponseDueAt != null && t.ResponseDueAt >= now)
                    || (t.ResolutionDueAt != null && t.ResolutionDueAt >= now)))
            .ToListAsync(ct);

        var warned = false;
        foreach (var ticket in approaching)
        {
            var dueDates = new[] { ticket.ResponseDueAt, ticket.ResolutionDueAt }
                .Where(d => d.HasValue).Select(d => d!.Value);

            var isApproaching = dueDates.Any(due =>
            {
                var totalWindow = (due - ticket.CreatedAt).TotalSeconds;
                if (totalWindow <= 0)
                    return true; // a degenerate (zero/negative) window counts as "approaching"
                var remaining = (due - now).TotalSeconds;
                return remaining / totalWindow < 0.2;
            });

            if (isApproaching)
            {
                ticket.Warn();
                warned = true;
            }
        }

        if (warned)
        {
            await db.SaveChangesAsync(ct);
        }
```

(`EvaluatedStatuses` and `now` are already declared at the top of `ScanAsync` — reuse them; do not
redeclare.) The existing breach `SaveChangesAsync` call (`if (recorded > 0) …`) and this new one touch
disjoint ticket sets (a ticket that just breached is never in `approaching`, since breach requires the
due date to have passed and `approaching` requires it not to), so two saves are correct and safe.

- [ ] **Step 5: Integration test against the real scanner**

Model exactly on `SlaPauseAndEscalationEndpointTests` (CrmApiFactory, real LocalDB, resolve
`ISlaBreachScanner` from a scope and call `ScanAsync()` directly — do **not** wait on the
`SlaBreachDetector` timer):

```csharp
// backend/tests/CustomerSupport.Tests/Integration/SlaWarningEndpointTests.cs (excerpt)
public class SlaWarningEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;
    private Guid _categoryId, _customerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_admin, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        _categoryId = await _factory.EnsureCategoryAsync("Technical");
        var customer = await _admin.PostAsJsonAsync("/api/Customers", new
        {
            name = "Warn Tester",
            email = $"warn-{Guid.NewGuid():N}@example.com",
            phone = (string?)null,
        });
        _customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    public Task DisposeAsync() { _admin.Dispose(); return _factory.DisposeAsync().AsTask(); }

    private async Task<Guid> CreateTicketAsync()
    {
        var r = await _admin.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Warning fixture", description = "x",
            customerId = _customerId, categoryId = _categoryId, priority = "Normal",
        });
        return (await r.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    [Fact]
    [Trait("AC", "229")]
    public async Task AC229_TicketApproachingDueDate_IsMarkedWarning()
    {
        var ticketId = await CreateTicketAsync();

        // Due ~1h out, created now -> remaining/total ≈ 1.0, NOT yet warning. Push the created-at
        // back so the window is mostly elapsed: set CreatedAt 23h ago, due 1h from now => 1/24 < 0.2.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var t = await db.Tickets.FirstAsync(x => x.Id == ticketId);
            var createdAt = DateTime.UtcNow.AddHours(-23);
            db.Entry(t).Property(x => x.CreatedAt).CurrentValue = createdAt;
            db.Entry(t).Property(x => x.ResponseDueAt).CurrentValue = DateTime.UtcNow.AddHours(1);
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>().ScanAsync();

        var after = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
        after!.Data!.EscalationState.Should().Be("Warning");
    }

    [Fact]
    [Trait("AC", "230")]
    public async Task AC230_AlreadyWarnings_StayWarning()
    {
        var ticketId = await CreateTicketAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var t = await db.Tickets.FirstAsync(x => x.Id == ticketId);
            db.Entry(t).Property(x => x.CreatedAt).CurrentValue = DateTime.UtcNow.AddHours(-23);
            db.Entry(t).Property(x => x.ResponseDueAt).CurrentValue = DateTime.UtcNow.AddHours(1);
            db.Entry(t).Property(x => x.EscalationState).CurrentValue = "Warning";
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>().ScanAsync();

        var after = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
        after!.Data!.EscalationState.Should().Be("Warning");
    }

    public sealed record TicketRow(Guid Id, string Status, string RowVersion, string EscalationState);
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketSlaWarningTests|FullyQualifiedName~SlaWarningEndpointTests|FullyQualifiedName~SlaPauseAndEscalationEndpointTests"`
Expected: PASS — new tests plus the existing escalation suite unaffected (Warning and Level1 are
mutually exclusive states reached from different scanner branches, so this must not regress
`AC138_*`/`AC139_*`).

- [ ] **Step 7: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs \
        backend/src/CustomerSupport.Infrastructure/Jobs/SlaBreachScanner.cs \
        backend/tests/CustomerSupport.Tests/Unit/Domain/TicketSlaWarningTests.cs \
        backend/tests/CustomerSupport.Tests/Integration/SlaWarningEndpointTests.cs
git commit -m "feat(sla): pre-breach warning state (US-217, AC-229..231)"
```

## Definition of done

`AC-229` through `AC-231` each covered by a test naming it · full suite green, no regression in the
existing escalation tests · task record written to
`docs/superpowers/plans/EPIC-05-US-217-sla-warning/README.md`. No frontend change needed — the queue's
escalation badge and countdown already render any `EscalationState` value including `"Warning"`, per
the `FEAT-17` addendum's own design (it was built to render "None"/"Warning"/"Level1"+ generically,
not to special-case `"Level1"`).
