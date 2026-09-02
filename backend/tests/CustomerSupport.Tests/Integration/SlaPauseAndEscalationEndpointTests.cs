using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Infrastructure.Jobs;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-17 (second slice) — SLA pause/resume on Pending, and single-level auto-escalation on
/// breach. `AC-134` through `AC-139`. Real LocalDB throughout.
/// </summary>
public class SlaPauseAndEscalationEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;
    private HttpClient _agent = null!;
    private Guid _agentId;
    private Guid _categoryId;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_admin, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        (_agent, var agentUser) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
        _agentId = agentUser.Id;
        _categoryId = await _factory.EnsureCategoryAsync("Technical");

        var customer = await _admin.PostAsJsonAsync("/api/Customers", new
        {
            name = "Sami Farid",
            email = $"sla-pause-{Guid.NewGuid():N}@example.com",
            phone = (string?)null,
        });
        _customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    public Task DisposeAsync()
    {
        _admin.Dispose();
        _agent.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task AssignAsync(Guid ticketId, Guid agentId)
    {
        var response = await _admin.PostAsJsonAsync(
            $"/api/Tickets/{ticketId}/assignee",
            new { assigneeId = agentId, rowVersion = await RowVersionAsync(ticketId) });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<Guid> CreateTicketAsync()
    {
        var response = await _admin.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "SLA pause test ticket",
            description = "Exercising pause/resume and escalation.",
            customerId = _customerId,
            categoryId = _categoryId,
            impact = "Medium",
            urgency = "Medium",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    private async Task<string> RowVersionAsync(Guid ticketId)
    {
        var ticket = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
        return ticket!.Data!.RowVersion;
    }

    private async Task ChangeStatusAsync(Guid ticketId, string status)
    {
        var response = await _admin.PostAsJsonAsync(
            $"/api/Tickets/{ticketId}/status", new { status, rowVersion = await RowVersionAsync(ticketId) });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- AC-134/135 — pause on Waiting for Customer, resume on exit ----------------------------------------

    [Fact]
    [Trait("AC", "134")]
    public async Task AC134_TransitionToWaitingForCustomer_SetsPausedAt()
    {
        var ticketId = await CreateTicketAsync();
        await ChangeStatusAsync(ticketId, "Open");
        await AssignAsync(ticketId, _agentId);
        await WalkToAsync(ticketId, "Assigned", "In Progress", "Waiting for Customer");

        var ticket = await LoadTicketAsync(ticketId);

        ticket.PausedAt.Should().NotBeNull();
        ticket.TotalPausedSeconds.Should().Be(0);
    }

    [Fact]
    [Trait("AC", "135")]
    public async Task AC135_ExitingWaitingForCustomer_AccumulatesPausedSecondsAndShiftsDueDates()
    {
        var ticketId = await CreateTicketAsync();
        var originalDue = DateTime.UtcNow.AddHours(4);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId);
            db.Entry(ticket).Property(t => t.ResolutionDueAt).CurrentValue = originalDue;
            await db.SaveChangesAsync();
        }

        await ChangeStatusAsync(ticketId, "Open");
        await AssignAsync(ticketId, _agentId);
        await WalkToAsync(ticketId, "Assigned", "In Progress", "Waiting for Customer");
        await Task.Delay(1100);
        await ChangeStatusAsync(ticketId, "In Progress");

        var ticket2 = await LoadTicketAsync(ticketId);
        ticket2.PausedAt.Should().BeNull();
        ticket2.TotalPausedSeconds.Should().BeGreaterThan(0);
        ticket2.ResolutionDueAt.Should().NotBeNull();
        ticket2.ResolutionDueAt!.Value.Should().BeAfter(originalDue);
    }

    [Fact]
    [Trait("AC", "136")]
    public async Task AC136_MultipleWaitingCycles_AccumulatePausedSeconds()
    {
        var ticketId = await CreateTicketAsync();
        await ChangeStatusAsync(ticketId, "Open");
        await AssignAsync(ticketId, _agentId);
        await WalkToAsync(ticketId, "Assigned", "In Progress");

        await ChangeStatusAsync(ticketId, "Waiting for Customer");
        await Task.Delay(1100);
        await ChangeStatusAsync(ticketId, "In Progress");
        var afterFirst = (await LoadTicketAsync(ticketId)).TotalPausedSeconds;

        await ChangeStatusAsync(ticketId, "Waiting for Customer");
        await Task.Delay(1100);
        await ChangeStatusAsync(ticketId, "In Progress");
        var afterSecond = (await LoadTicketAsync(ticketId)).TotalPausedSeconds;

        afterFirst.Should().BeGreaterThan(0);
        afterSecond.Should().BeGreaterThan(afterFirst);
    }

    private async Task WalkToAsync(Guid ticketId, params string[] steps)
    {
        foreach (var step in steps)
        {
            await ChangeStatusAsync(ticketId, step);
        }
    }

    // --- AC-137/138/139 — escalation ------------------------------------------------------------------

    [Fact]
    [Trait("AC", "137")]
    public async Task AC137_NewTicket_EscalationStateIsNone()
    {
        var ticketId = await CreateTicketAsync();

        var ticket = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");

        ticket!.Data!.EscalationState.Should().Be("None");
    }

    [Fact]
    [Trait("AC", "138")]
    public async Task AC138_FirstBreach_EscalatesTicketToLevel1()
    {
        var ticketId = await CreateTicketAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId);
            var past = DateTime.UtcNow.AddHours(-1);
            db.Entry(ticket).Property(t => t.ResponseDueAt).CurrentValue = past;
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var scanner = scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>();
            (await scanner.ScanAsync()).Should().BeGreaterThan(0);
        }

        var ticketAfter = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");

        ticketAfter!.Data!.EscalationState.Should().Be("Level1");
    }

    [Fact]
    [Trait("AC", "139")]
    public async Task AC139_SecondBreachOnAnAlreadyEscalatedTicket_AdvancesTheLevel()
    {
        var ticketId = await CreateTicketAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId);
            var past = DateTime.UtcNow.AddHours(-1);
            db.Entry(ticket).Property(t => t.ResponseDueAt).CurrentValue = past;
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var scanner = scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>();
            await scanner.ScanAsync(); // first breach — escalates to Level1
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId);
            var past = DateTime.UtcNow.AddHours(-1);
            db.Entry(ticket).Property(t => t.ResolutionDueAt).CurrentValue = past; // a second, different target type
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var scanner = scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>();
            (await scanner.ScanAsync()).Should().BeGreaterThan(0); // the resolution breach advances it further
        }

        var ticketAfter = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");

        // US-218 redefines AC-139: a *new* breach (distinct target type) on an already-escalated
        // ticket now advances it up the ladder (Level1 -> Level2) rather than leaving it unchanged.
        // The single-level "only escalate from None" rule is superseded by multi-level progression.
        ticketAfter!.Data!.EscalationState.Should().Be("Level2");
    }

    private async Task<TicketDbRow> LoadTicketAsync(Guid ticketId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await db.Tickets.AsNoTracking().FirstAsync(t => t.Id == ticketId);
        return new TicketDbRow(ticket.PausedAt, ticket.TotalPausedSeconds, ticket.ResolutionDueAt);
    }

    private sealed record TicketDbRow(DateTime? PausedAt, int TotalPausedSeconds, DateTime? ResolutionDueAt);

    public sealed record TicketRow(Guid Id, string Status, string RowVersion, string EscalationState);
}
