using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Entities.Identity;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-19+ — ticket volume, SLA performance and agent performance reports. `AC-148` through
/// `AC-154`. Real LocalDB throughout.
/// </summary>
public class ReportsEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _supervisor = null!;
    private HttpClient _agent = null!;
    private Guid _categoryId;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_supervisor, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Supervisor);
        (_agent, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
        _categoryId = await _factory.EnsureCategoryAsync("Technical");

        var customer = await _supervisor.PostAsJsonAsync("/api/Customers", new
        {
            name = "Report Fixture",
            email = $"reports-{Guid.NewGuid():N}@example.com",
            phone = (string?)null,
        });
        _customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    public Task DisposeAsync()
    {
        _supervisor.Dispose();
        _agent.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> CreateTicketAsync(string priority)
    {
        var response = await _supervisor.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Report fixture ticket",
            description = "Exercising the reporting endpoints.",
            customerId = _customerId,
            categoryId = _categoryId,
            priority,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    private static string Range() =>
        $"from={DateTime.UtcNow.AddDays(-1):O}&to={DateTime.UtcNow.AddDays(1):O}";

    // --- AC-148 — authorization -------------------------------------------------------------------

    [Fact]
    [Trait("AC", "148")]
    public async Task AC148_Agent_CannotReadTicketVolumeReport()
    {
        var response = await _agent.GetAsync($"/api/reports/ticket-volume?{Range()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("AC", "148")]
    public async Task AC148_Unauthenticated_CannotReadTicketVolumeReport()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/reports/ticket-volume?{Range()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- AC-149/150/151 — ticket volume -------------------------------------------------------------

    [Fact]
    [Trait("AC", "149")]
    public async Task AC149_TicketVolume_GroupsByPeriod()
    {
        await CreateTicketAsync("High");
        await CreateTicketAsync("High");

        var report = await _supervisor.GetFromJsonAsync<Response<TicketVolumeReportRow>>(
            $"/api/reports/ticket-volume?{Range()}&groupBy=day");

        report!.Data!.ByPeriod.Sum(b => b.Count).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    [Trait("AC", "150")]
    public async Task AC150_TicketVolume_GroupsByCategory()
    {
        await CreateTicketAsync("Low");

        var report = await _supervisor.GetFromJsonAsync<Response<TicketVolumeReportRow>>(
            $"/api/reports/ticket-volume?{Range()}");

        report!.Data!.ByCategory.Should().Contain(b => b.Key == "Technical" && b.Count >= 1);
    }

    [Fact]
    [Trait("AC", "151")]
    public async Task AC151_TicketVolume_GroupsByPriority()
    {
        await CreateTicketAsync("Urgent");

        var report = await _supervisor.GetFromJsonAsync<Response<TicketVolumeReportRow>>(
            $"/api/reports/ticket-volume?{Range()}");

        report!.Data!.ByPriority.Should().Contain(b => b.Key == "Urgent" && b.Count >= 1);
    }

    // --- AC-154 — bad range ------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "154")]
    public async Task AC154_FromAfterTo_Returns400KeyedToField()
    {
        var from = DateTime.UtcNow;
        var to = DateTime.UtcNow.AddDays(-1);

        var response = await _supervisor.GetAsync(
            $"/api/reports/ticket-volume?from={from:O}&to={to:O}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == "To");
    }

    // --- AC-152 — SLA performance ------------------------------------------------------------------

    [Fact]
    [Trait("AC", "152")]
    public async Task AC152_SlaPerformance_MetPlusBreachedNeverExceedsTotal()
    {
        // No SLAPolicy is required to exist for this assertion to hold: a ticket with no due date
        // simply is not counted (spec A6), so the identity holds regardless of fixture data already
        // created by other tests in this class.
        await CreateTicketAsync("Normal");

        var report = await _supervisor.GetFromJsonAsync<Response<SlaPerformanceReportRow>>(
            $"/api/reports/sla-performance?{Range()}");

        foreach (var row in report!.Data!.ByPriority)
        {
            (row.MetFirstResponse + row.BreachedFirstResponse).Should().BeLessOrEqualTo(row.Total);
            (row.MetResolution + row.BreachedResolution).Should().BeLessOrEqualTo(row.Total);
        }
    }

    // --- AC-153 — agent performance ----------------------------------------------------------------

    [Fact]
    [Trait("AC", "153")]
    public async Task AC153_AgentPerformance_CountsResolvedTicketsPerAgent()
    {
        var (agentClient, agentUser) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
        var ticketId = await CreateTicketAsync("Normal");

        var detail = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");
        (await _supervisor.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee",
            new { assigneeId = agentUser.Id, rowVersion = detail!.Data!.RowVersion }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var afterAssign = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");
        (await agentClient.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
            new { status = "Open", rowVersion = afterAssign!.Data!.RowVersion }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var afterOpen = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");
        (await agentClient.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
            new { status = "Resolved", rowVersion = afterOpen!.Data!.RowVersion }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var report = await _supervisor.GetFromJsonAsync<Response<AgentPerformanceReportRow>>(
            $"/api/reports/agent-performance?{Range()}");

        report!.Data!.ByAgent.Should().Contain(r => r.AgentId == agentUser.Id && r.TicketsResolved >= 1);

        agentClient.Dispose();
    }

    public sealed record ReportBucketRow(string Key, int Count);
    public sealed record TicketVolumeReportRow(
        IReadOnlyList<ReportBucketRow> ByPeriod,
        IReadOnlyList<ReportBucketRow> ByCategory,
        IReadOnlyList<ReportBucketRow> ByPriority);

    public sealed record SlaPerformanceRowFixture(
        string Priority, int Total, int MetFirstResponse, int BreachedFirstResponse,
        int MetResolution, int BreachedResolution);
    public sealed record SlaPerformanceReportRow(IReadOnlyList<SlaPerformanceRowFixture> ByPriority);

    public sealed record TicketDetailRow(string RowVersion);
    public sealed record AgentPerformanceRowFixture(Guid AgentId, string AgentName, int TicketsResolved, double AvgHandleMinutes);
    public sealed record AgentPerformanceReportRow(IReadOnlyList<AgentPerformanceRowFixture> ByAgent);
}
