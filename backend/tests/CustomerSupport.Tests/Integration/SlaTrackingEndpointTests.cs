using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Infrastructure.Jobs;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-17 (first slice) — SLA policies, target computation and breach detection.
/// `AC-124` through `AC-133`. Real LocalDB throughout.
/// </summary>
public class SlaTrackingEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;
    private HttpClient _agent = null!;
    private Guid _categoryId;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_admin, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        (_agent, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
        _categoryId = await _factory.EnsureCategoryAsync("Technical");

        var customer = await _admin.PostAsJsonAsync("/api/Customers", new
        {
            name = "Omar Nasser",
            email = $"sla-{Guid.NewGuid():N}@example.com",
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

    private Task<HttpResponseMessage> CreatePolicyAsync(
        string priority, decimal responseHours, decimal resolutionHours, Guid? categoryId = null, Guid? branchId = null) =>
        _admin.PostAsJsonAsync("/api/SLAPolicies", new
        {
            priority,
            responseTargetHours = responseHours,
            resolutionTargetHours = resolutionHours,
            categoryId,
            branchId,
        });

    private async Task<Guid> CreateTicketAsync(string priority)
    {
        var response = await _admin.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "SLA test ticket",
            description = "Exercising SLA target computation.",
            customerId = _customerId,
            categoryId = _categoryId,
            priority,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    private async Task<TicketRow> GetTicketAsync(Guid id)
    {
        var response = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{id}");
        return response!.Data!;
    }

    // --- AC-124/125/126 — creating a policy -------------------------------------------------------

    [Fact]
    [Trait("AC", "124")]
    public async Task AC124_CreatePolicy_ValidFields_Returns201()
    {
        var response = await CreatePolicyAsync("Urgent", 1, 4);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        body!.Data.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("AC", "125")]
    public async Task AC125_NonAdmin_CannotCreatePolicy()
    {
        var response = await _agent.PostAsJsonAsync("/api/SLAPolicies", new
        {
            priority = "Urgent",
            responseTargetHours = 1m,
            resolutionTargetHours = 4m,
            categoryId = (Guid?)null,
            branchId = (Guid?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("AC", "126")]
    public async Task AC126_CreatePolicy_ZeroResponseTarget_Returns400KeyedToField()
    {
        var response = await CreatePolicyAsync("Urgent", 0, 4);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == "ResponseTargetHours");
    }

    [Fact]
    [Trait("AC", "127")]
    public async Task AC127_GetPolicies_ReturnsAPagedList()
    {
        (await CreatePolicyAsync("Low", 8, 48)).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await _admin.GetFromJsonAsync<Response<PagedRow<PolicyRow>>>("/api/SLAPolicies");

        response!.Data!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PolicyFullCrudCycle()
    {
        var create = await CreatePolicyAsync("High", 4, 8);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await create.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var update = await _admin.PutAsJsonAsync($"/api/SLAPolicies/{id}", new
        {
            priority = "High",
            responseTargetHours = 2m,
            resolutionTargetHours = 6m,
            categoryId = (Guid?)null,
            branchId = (Guid?)null,
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await _admin.DeleteAsync($"/api/SLAPolicies/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateUnknownPolicy_Returns404()
    {
        var response = await _admin.PutAsJsonAsync($"/api/SLAPolicies/{Guid.NewGuid()}", new
        {
            priority = "High",
            responseTargetHours = 2m,
            resolutionTargetHours = 6m,
            categoryId = (Guid?)null,
            branchId = (Guid?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivateUnknownPolicy_Returns404()
    {
        var response = await _admin.DeleteAsync($"/api/SLAPolicies/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonAdmin_CannotUpdateOrDeactivatePolicy()
    {
        var create = await CreatePolicyAsync("Urgent", 1, 4);
        var id = (await create.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var update = await _agent.PutAsJsonAsync($"/api/SLAPolicies/{id}", new
        {
            priority = "Urgent",
            responseTargetHours = 1m,
            resolutionTargetHours = 4m,
            categoryId = (Guid?)null,
            branchId = (Guid?)null,
        });
        var delete = await _agent.DeleteAsync($"/api/SLAPolicies/{id}");

        update.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- AC-128/129/130 — computing targets at ticket creation -------------------------------------

    [Fact]
    [Trait("AC", "128")]
    public async Task AC128_CreateTicket_MatchingPolicy_ComputesDueDates()
    {
        var priority = $"High"; // shared across tests in this class; policy below is unscoped to this run only via category
        var before = DateTime.UtcNow;
        (await CreatePolicyAsync(priority, 2, 24, categoryId: _categoryId)).StatusCode
            .Should().Be(HttpStatusCode.Created);

        var ticketId = await CreateTicketAsync(priority);
        var ticket = await GetTicketAsync(ticketId);

        ticket.ResponseDueAt.Should().NotBeNull();
        ticket.ResolutionDueAt.Should().NotBeNull();
        ticket.ResponseDueAt!.Value.Should().BeCloseTo(before.AddHours(2), TimeSpan.FromMinutes(1));
        ticket.ResolutionDueAt!.Value.Should().BeCloseTo(before.AddHours(24), TimeSpan.FromMinutes(1));
    }

    [Fact]
    [Trait("AC", "129")]
    public async Task AC129_CreateTicket_NoMatchingPolicy_DueDatesRemainNull()
    {
        // A category of this test's own, so no policy created elsewhere in the class can match it.
        var isolatedCategoryId = await _factory.EnsureCategoryAsync($"NoSlaPolicy-{Guid.NewGuid():N}");

        var response = await _admin.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "No policy for this one",
            description = "Nothing should match.",
            customerId = _customerId,
            categoryId = isolatedCategoryId,
            priority = "Normal",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticketId = (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var ticket = await GetTicketAsync(ticketId);

        ticket.ResponseDueAt.Should().BeNull();
        ticket.ResolutionDueAt.Should().BeNull();
    }

    [Fact]
    [Trait("AC", "130")]
    public async Task AC130_CreateTicket_CategoryScopedPolicyWinsOverUnscoped()
    {
        var priority = "Low";
        var scopedCategoryId = await _factory.EnsureCategoryAsync($"Scoped-{Guid.NewGuid():N}");

        (await CreatePolicyAsync(priority, 24, 96)).StatusCode.Should().Be(HttpStatusCode.Created); // unscoped, wide targets
        (await CreatePolicyAsync(priority, 1, 4, categoryId: scopedCategoryId)).StatusCode
            .Should().Be(HttpStatusCode.Created); // scoped, narrow targets

        var before = DateTime.UtcNow;
        var response = await _admin.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Scoped policy should win",
            description = "The category-specific policy applies.",
            customerId = _customerId,
            categoryId = scopedCategoryId,
            priority,
        });
        var ticketId = (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var ticket = await GetTicketAsync(ticketId);

        // The narrow (scoped) policy's 1-hour target, not the wide (unscoped) 24-hour one.
        ticket.ResponseDueAt!.Value.Should().BeCloseTo(before.AddHours(1), TimeSpan.FromMinutes(1));
    }

    // --- AC-131/132/133 — breach detection ----------------------------------------------------------

    [Fact]
    [Trait("AC", "131")]
    public async Task AC131_OverdueOpenTicket_IsRecordedAsBreached()
    {
        var ticketId = await CreateTicketAsync("Normal"); // no policy needed — due dates set directly below

        await SetDueDatesInThePastAsync(ticketId);

        var recorded = await RunScannerAsync();

        recorded.Should().BeGreaterThan(0);
        (await BreachEventsAsync(ticketId)).Should().Contain(e => e.TargetType == "Response" && e.BreachedAt != null);
        (await BreachEventsAsync(ticketId)).Should().Contain(e => e.TargetType == "Resolution" && e.BreachedAt != null);
    }

    [Fact]
    [Trait("AC", "132")]
    public async Task AC132_RunningTwice_DoesNotDuplicateTheBreachEvent()
    {
        var ticketId = await CreateTicketAsync("Normal");
        await SetDueDatesInThePastAsync(ticketId);

        await RunScannerAsync();
        var secondPassRecorded = await RunScannerAsync();

        secondPassRecorded.Should().Be(0);
        (await BreachEventsAsync(ticketId)).Where(e => e.TargetType == "Response").Should().HaveCount(1);
    }

    [Fact]
    [Trait("AC", "133")]
    public async Task AC133_WaitingForCustomerTicket_IsNotEvaluated()
    {
        var ticketId = await CreateTicketAsync("Normal");
        await SetDueDatesInThePastAsync(ticketId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId);
            db.Entry(ticket).Property(t => t.Status).CurrentValue = "Waiting for Customer";
            await db.SaveChangesAsync();
        }

        await RunScannerAsync();

        (await BreachEventsAsync(ticketId)).Should().BeEmpty();
    }

    private async Task SetDueDatesInThePastAsync(Guid ticketId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId);

        var past = DateTime.UtcNow.AddHours(-1);
        db.Entry(ticket).Property(t => t.ResponseDueAt).CurrentValue = past;
        db.Entry(ticket).Property(t => t.ResolutionDueAt).CurrentValue = past;
        await db.SaveChangesAsync();
    }

    private async Task<int> RunScannerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var scanner = scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>();
        return await scanner.ScanAsync();
    }

    private async Task<List<(string TargetType, DateTime? BreachedAt)>> BreachEventsAsync(Guid ticketId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Set<SLAEvent>().IgnoreQueryFilters()
            .Where(e => e.TicketId == ticketId)
            .Select(e => new ValueTuple<string, DateTime?>(e.TargetType, e.BreachedAt))
            .ToListAsync();
    }

    public sealed record TicketRow(Guid Id, string Status, DateTime? ResponseDueAt, DateTime? ResolutionDueAt);
    public sealed record PolicyRow(Guid Id, string Priority, decimal ResponseTargetHours, decimal ResolutionTargetHours);
    public sealed record PagedRow<T>(IReadOnlyList<T> Items, int PageIndex, int PageSize, int TotalCount);
}
