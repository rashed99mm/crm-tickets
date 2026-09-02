using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>US-922 — the wire half of resolution discipline (AC-922.1/2/3/4/6).</summary>
public class TicketResolutionEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _supervisor = null!;
    private HttpClient _agent = null!;
    private Guid _categoryId;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_supervisor, _) = await _factory.CreateAuthenticatedClientAsync("Supervisor");
        (_agent, _) = await _factory.CreateAuthenticatedClientAsync("Agent");
        _categoryId = await _factory.EnsureCategoryAsync("Technical");

        var customer = await _supervisor.PostAsJsonAsync("/api/Customers", new
        {
            name = "Layla Haddad",
            email = $"resolution-{Guid.NewGuid():N}@example.com",
        });
        _customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    public Task DisposeAsync()
    {
        _supervisor.Dispose();
        _agent.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> TicketAtOpenAsync()
    {
        var created = await _supervisor.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Cannot sign in",
            description = "The portal rejects my password.",
            customerId = _customerId,
            categoryId = _categoryId,
            impact = "Medium",
            urgency = "Medium",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
        (await ChangeStatusAsync(id, "Open")).StatusCode.Should().Be(HttpStatusCode.OK);
        return id;
    }

    private async Task<string> RowVersionAsync(Guid id)
    {
        var detail = await _supervisor.GetFromJsonAsync<Response<TicketResolutionDetail>>($"/api/Tickets/{id}");
        return detail!.Data!.RowVersion;
    }

    private async Task<HttpResponseMessage> ChangeStatusAsync(
        Guid id, string status, string? resolutionCode = null, string? resolutionNotes = null)
    {
        var rowVersion = await RowVersionAsync(id);
        return await _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/status",
            new { status, rowVersion, resolutionCode, resolutionNotes });
    }

    [Fact]
    [Trait("AC", "922.1")]
    public async Task Resolving_Without_Code_Or_Notes_Is_A_400_Naming_Both_Fields()
    {
        var id = await TicketAtOpenAsync();

        var response = await ChangeStatusAsync(id, "Resolved");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        body!.Errors.Should().Contain(e => e.Field == "ResolutionCode");
        body.Errors.Should().Contain(e => e.Field == "ResolutionNotes");
    }

    [Fact]
    [Trait("AC", "922.3")]
    public async Task An_Unknown_Resolution_Code_Is_A_400_Naming_The_Field()
    {
        var id = await TicketAtOpenAsync();

        var response = await ChangeStatusAsync(id, "Resolved", "Solved", "notes");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        body!.Errors.Should().Contain(e => e.Field == "ResolutionCode");
    }

    [Fact]
    [Trait("AC", "922.2")]
    [Trait("AC", "922.6")]
    public async Task A_Valid_Resolve_Stamps_And_The_Detail_Carries_It()
    {
        var id = await TicketAtOpenAsync();

        var response = await ChangeStatusAsync(id, "Resolved", "Workaround", "Cleared the cache as a stopgap.");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await _supervisor.GetFromJsonAsync<Response<TicketResolutionDetail>>($"/api/Tickets/{id}");
        detail!.Data!.ResolutionCode.Should().Be("Workaround");
        detail.Data.ResolutionNotes.Should().Be("Cleared the cache as a stopgap.");
        detail.Data.ReopenCount.Should().Be(0);
        detail.Data.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("AC", "922.4")]
    public async Task Reopening_Clears_The_Resolution_And_Counts()
    {
        var id = await TicketAtOpenAsync();
        (await ChangeStatusAsync(id, "Resolved", "Fixed", "Reset the password.")).StatusCode.Should().Be(HttpStatusCode.OK);

        // A reopen needs an assignee to enter In Progress (AC-505): the ticket was never assigned — assign first.
        var agents = await _supervisor.GetFromJsonAsync<Response<List<AssignableAgent>>>("/api/Tickets/assignable-agents");
        var rowVersion = await RowVersionAsync(id);
        (await _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/assignee",
            new { assigneeId = agents!.Data![0].Id, rowVersion })).StatusCode.Should().Be(HttpStatusCode.OK);

        var reopen = await ChangeStatusAsync(id, "In Progress");

        reopen.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await _supervisor.GetFromJsonAsync<Response<TicketResolutionDetail>>($"/api/Tickets/{id}");
        detail!.Data!.ResolutionCode.Should().BeNull();
        detail.Data.ResolutionNotes.Should().BeNull();
        detail.Data.ReopenCount.Should().Be(1);
    }

    private sealed record TicketResolutionDetail(
        Guid Id, string Status, string RowVersion,
        string? ResolutionCode, string? ResolutionNotes, int ReopenCount, DateTime? ResolvedAt);

    private sealed record AssignableAgent(Guid Id, string Name, string Email);
}
