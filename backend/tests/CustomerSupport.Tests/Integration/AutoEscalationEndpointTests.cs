using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.ValueObjects;
using CustomerSupport.Infrastructure.Jobs;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Shared.Contracts;
using CustomerSupport.Shared.Contracts.Messages;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// US-218 — multi-level auto-escalation progression (AC-218.1..AC-218.3). Real LocalDB, each test
/// drives the scanner one pass and asserts on the persisted escalation level and its appended
/// <c>Escalated</c> history, exactly the way the AC-131..AC-139 slices do.
/// </summary>
public class AutoEscalationEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;
    private Guid _categoryId;
    private Guid _customerId;
    private Guid _agentId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_admin, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        _categoryId = await _factory.EnsureCategoryAsync("EscalationTech");

        var customer = await _admin.PostAsJsonAsync("/api/Customers", new
        {
            name = "US218 Customer",
            email = $"us218-{Guid.NewGuid():N}@example.com",
            phone = (string?)null,
        });
        customer.StatusCode.Should().Be(HttpStatusCode.Created);
        _customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var agentEmail = $"escalation-agent-{Guid.NewGuid():N}@example.com";
        var agentResponse = await _admin.PostAsJsonAsync("/api/Users", new
        {
            email = agentEmail,
            username = "escalationagent" + Guid.NewGuid().ToString("N"),
            password = "Agent@123456",
            firstName = "Escalation",
            lastName = "Agent",
            roles = new[] { "Agent" },
        });
        agentResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        _agentId = (await agentResponse.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    public Task DisposeAsync()
    {
        _admin.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> CreateTicketAsync()
    {
        var response = await _admin.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "US-218 escalation test ticket",
            description = "Exercising multi-level automatic escalation.",
            customerId = _customerId,
            categoryId = _categoryId,
            priority = "High",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    /// <summary>Runs one scanner pass from a fresh scope (the way the hosted service would).</summary>
    private async Task<int> ScanAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>().ScanAsync();
    }

    /// <summary>Backdates a due date so the next scanner pass sees a breach. Uses the given host's
    /// database (the shared one, unless a capturing factory was created against the same DB).</summary>
    private static async Task BackdateAsync(IServiceProvider services, Guid ticketId, bool resolution)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId);
        var past = DateTime.UtcNow.AddHours(-1);
        if (resolution)
        {
            db.Entry(ticket).Property(t => t.ResolutionDueAt).CurrentValue = past;
        }
        else
        {
            db.Entry(ticket).Property(t => t.ResponseDueAt).CurrentValue = past;
        }

        await db.SaveChangesAsync();
    }

    private async Task BackdateAsync(Guid ticketId, bool resolution)
        => await BackdateAsync(_factory.Services, ticketId, resolution);

    private async Task<int> EscalatedHistoryCountAsync(Guid ticketId)
        => await CountEscalatedHistoryAsync(_factory.Services, ticketId);

    private static async Task<int> CountEscalatedHistoryAsync(IServiceProvider services, Guid ticketId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await db.Tickets.Include(t => t.History).AsNoTracking().FirstAsync(t => t.Id == ticketId);
        return ticket.History.Count(h => h.ChangeType == TicketChangeType.Escalated.Value);
    }

    // --- AC-218.1 — first breach advances to the lowest configured level and records history -------

    [Fact]
    [Trait("AC", "218.1")]
    public async Task AC2181_BreachScanner_SetsLevel1AndAppendsHistory()
    {
        var ticketId = await CreateTicketAsync();
        await BackdateAsync(ticketId, resolution: false);

        (await ScanAsync()).Should().BeGreaterThan(0);

        var ticket = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
        ticket!.Data!.EscalationState.Should().Be("Level1");
        (await EscalatedHistoryCountAsync(ticketId)).Should().Be(1);
    }

    // --- AC-218.2 — a further qualifying breach advances to the next level and publishes on it -----

    [Fact]
    [Trait("AC", "218.2")]
    public async Task AC2182_SecondQualifyingBreach_SetsLevel2AndPublishesRoleTarget()
    {
        var ticketId = await CreateTicketAsync();
        await BackdateAsync(ticketId, resolution: false);
        (await ScanAsync()).Should().BeGreaterThan(0); // -> Level1

        await using var capturing = new CapturingPublisherFactory();

        // A *different* target type breaches, so AC-132 records it as new rather than a repeat,
        // and this pass advances the already-level-one ticket to Level2 while publishing.
        await BackdateAsync(capturing.Services, ticketId, resolution: true);
        using (var scope = capturing.Services.CreateScope())
        {
            var scanner = scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>();
            (await scanner.ScanAsync()).Should().BeGreaterThan(0);
        }

        var ticket = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
        ticket!.Data!.EscalationState.Should().Be("Level2");

        // Exactly one message was published for the Level1 -> Level2 advance, carrying the next
        // level's role so a consumer can route without another lookup.
        var message = capturing.Published.Should().ContainSingle().Subject;
        message.PreviousLevel.Should().Be("Level1");
        message.NextLevel.Should().Be("Level2");
        message.TargetRole.Should().Be("Supervisor");
        (await CountEscalatedHistoryAsync(capturing.Services, ticketId)).Should().Be(2);
    }

    [Fact]
    [Trait("AC", "218.2")]
    public async Task AC2182_TerminalLevel_DoesNotCreateFurtherHistory()
    {
        var ticketId = await CreateTicketAsync();
        await BackdateAsync(ticketId, resolution: false);
        (await ScanAsync()).Should().BeGreaterThan(0); // -> Level1
        await BackdateAsync(ticketId, resolution: true);
        (await ScanAsync()).Should().BeGreaterThan(0); // -> Level2 (terminal: highest configured)

        // Level2 is the highest configured level, so no further escalation is possible. Running the
        // scanner again — even over the still-breached ticket — must neither advance the level nor
        // append another Escalated history row.
        var historyBefore = await EscalatedHistoryCountAsync(ticketId);
        await ScanAsync();

        var ticket = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
        ticket!.Data!.EscalationState.Should().Be("Level2");
        (await EscalatedHistoryCountAsync(ticketId)).Should().Be(historyBefore);
    }

    // --- AC-218.3 — idempotency under concurrency and the paused/resolved guard --------------------

    [Fact]
    [Trait("AC", "218.3")]
    public async Task AC2183_ConcurrentScannerRuns_CreateOneTransition()
    {
        var ticketId = await CreateTicketAsync();
        await BackdateAsync(ticketId, resolution: false);

        // Two independent passes over the same breached ticket, each with its own context, run
        // concurrently so they contend on the ticket's RowVersion at the database. Exactly one may
        // win the escalation; the loser must resolve to a no-op (AC-218.3) rather than a duplicate.
        using var scopeA = _factory.Services.CreateScope();
        using var scopeB = _factory.Services.CreateScope();
        var scannerA = scopeA.ServiceProvider.GetRequiredService<ISlaBreachScanner>();
        var scannerB = scopeB.ServiceProvider.GetRequiredService<ISlaBreachScanner>();

        var results = await Task.WhenAll(scannerA.ScanAsync(), scannerB.ScanAsync());
        results.Sum().Should().BeGreaterThan(0);

        var ticket = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
        ticket!.Data!.EscalationState.Should().Be("Level1");
        (await EscalatedHistoryCountAsync(ticketId)).Should().Be(1);
    }

    [Theory]
    [Trait("AC", "218.3")]
    [InlineData("Waiting for Customer")]
    [InlineData("Resolved")]
    public async Task AC2183_WaitingOrResolvedTicket_DoesNotEscalate(string status)
    {
        var ticketId = await CreateTicketAsync();
        await BackdateAsync(ticketId, resolution: false);

        await MoveToStatusAsync(ticketId, status);

        (await ScanAsync()).Should().Be(0);

        var after = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
        after!.Data!.EscalationState.Should().Be("None");
        (await EscalatedHistoryCountAsync(ticketId)).Should().Be(0);
    }

    /// <summary>
    /// Walks the lifecycle to <paramref name="target"/> through valid transitions, assigning when a
    /// work state is in the path (AC-505).
    /// </summary>
    private async Task MoveToStatusAsync(Guid ticketId, string target)
    {
        if (target is "Waiting for Customer" or "Waiting for Internal Team")
        {
            var assignResponse = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
            var assignRv = assignResponse!.Data!.RowVersion;
            await _admin.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee",
                new { assigneeId = _agentId, rowVersion = assignRv });

            foreach (var step in new[] { "Open", "Assigned", "In Progress", target })
            {
                var rvResponse = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
                var rv = rvResponse!.Data!.RowVersion;
                var r = await _admin.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
                    new { status = step, rowVersion = rv });
                r.EnsureSuccessStatusCode();
            }
        }
        else
        {
            foreach (var step in new[] { "Open", target })
            {
                var rvResponse = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
                var rv = rvResponse!.Data!.RowVersion;
                var r = await _admin.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
                    new { status = step, rowVersion = rv });
                r.EnsureSuccessStatusCode();
            }
        }
    }

    public sealed record TicketRow(Guid Id, string Status, string RowVersion, string EscalationState);
}

/// <summary>
/// A self-contained host for the single test that must observe what the scanner *publishes*. The
/// shared <see cref="CrmApiFactory"/> runs with messaging degraded to the no-op publisher, so it
/// cannot witness a message; this factory swaps in a capturing <see cref="IMessagePublisher"/>.
/// </summary>
internal sealed class CapturingPublisherFactory : WebApplicationFactory<Program>
{
    private readonly List<SlaEscalatedMessage> _published = new();

    public IReadOnlyList<SlaEscalatedMessage> Published => _published;
    public HttpClient Client => CreateClient();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", TestDatabase.ConnectionString);
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-at-least-32-characters-long");
        builder.UseSetting("Messaging:Required", "false");
        builder.UseSetting("FileStorage:RootPath",
            Path.Combine(Path.GetTempPath(), "customersupport-tests", Guid.NewGuid().ToString("N")));
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            var sink = _published;
            services.AddSingleton<IMessagePublisher>(new CapturingPublisher(sink));
        });
    }

    private sealed class CapturingPublisher(List<SlaEscalatedMessage> sink) : IMessagePublisher
    {
        public Task PublishAsync<T>(string topic, T message, CancellationToken ct = default) where T : class
        {
            if (topic == Topics.SlaEscalated && message is SlaEscalatedMessage sla)
            {
                sink.Add(sla);
            }

            return Task.CompletedTask;
        }
    }
}
