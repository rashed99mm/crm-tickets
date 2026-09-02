using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>US-925 — links on the wire, and the Duplicate-code rule they gate (AC-925.1/2/3/4/5).</summary>
public class TicketLinkEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _supervisor = null!;
    private Guid _categoryId;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_supervisor, _) = await _factory.CreateAuthenticatedClientAsync("Supervisor");
        _categoryId = await _factory.EnsureCategoryAsync("Technical");

        var customer = await _supervisor.PostAsJsonAsync("/api/Customers", new
        {
            name = "Layla Haddad",
            email = $"links-{Guid.NewGuid():N}@example.com",
        });
        _customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    public Task DisposeAsync()
    {
        _supervisor.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<(Guid Id, string Reference)> CreateTicketAsync()
    {
        var response = await _supervisor.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Cannot sign in",
            description = "The portal rejects my password.",
            customerId = _customerId,
            categoryId = _categoryId,
            impact = "Medium",
            urgency = "Medium",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
        var detail = await DetailAsync(id);
        return (id, detail.Reference);
    }

    private async Task<LinkedDetail> DetailAsync(Guid id) =>
        (await _supervisor.GetFromJsonAsync<Response<LinkedDetail>>($"/api/Tickets/{id}"))!.Data!;

    private Task<HttpResponseMessage> LinkAsync(Guid id, string linkType, string targetReference) =>
        _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/links", new { linkType, targetReference });

    private async Task<HttpResponseMessage> ResolveAsync(Guid id, string resolutionCode)
    {
        var detail = await DetailAsync(id);
        if (detail.Status == "New")
        {
            (await _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/status",
                new { status = "Open", rowVersion = detail.RowVersion })).StatusCode.Should().Be(HttpStatusCode.OK);
            detail = await DetailAsync(id);
        }

        return await _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/status", new
        {
            status = "Resolved",
            rowVersion = detail.RowVersion,
            resolutionCode,
            resolutionNotes = "Consolidated into the original ticket.",
        });
    }

    [Fact]
    [Trait("AC", "925.1")]
    [Trait("AC", "925.5")]
    public async Task A_Link_Is_Created_And_Visible_From_Both_Sides()
    {
        var (a, _) = await CreateTicketAsync();
        var (b, refB) = await CreateTicketAsync();

        var response = await LinkAsync(a, "RelatedTo", refB);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DetailAsync(a)).Links.Should().ContainSingle(l =>
            l.LinkType == "RelatedTo" && l.Direction == "Outbound" && l.OtherReference == refB);
        (await DetailAsync(b)).Links.Should().ContainSingle(l =>
            l.LinkType == "RelatedTo" && l.Direction == "Inbound");
    }

    [Fact]
    [Trait("AC", "925.1")]
    public async Task An_Unknown_Target_Reference_Is_A_400_On_The_Field()
    {
        var (a, _) = await CreateTicketAsync();

        var response = await LinkAsync(a, "RelatedTo", "TKT-999999");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        body!.Errors.Should().Contain(e => e.Field == "TargetReference");
    }

    [Fact]
    [Trait("AC", "925.1")]
    public async Task A_Self_Link_Is_A_400()
    {
        var (a, refA) = await CreateTicketAsync();

        var response = await LinkAsync(a, "RelatedTo", refA);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("AC", "925.1")]
    public async Task The_Same_Link_Twice_Is_A_409()
    {
        var (a, _) = await CreateTicketAsync();
        var (_, refB) = await CreateTicketAsync();
        (await LinkAsync(a, "RelatedTo", refB)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await LinkAsync(a, "RelatedTo", refB);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("AC", "925.2")]
    public async Task A_Direct_Duplicate_Cycle_Is_A_409()
    {
        var (a, refA) = await CreateTicketAsync();
        var (b, refB) = await CreateTicketAsync();
        (await LinkAsync(a, "DuplicateOf", refB)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await LinkAsync(b, "DuplicateOf", refA);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("AC", "925.3")]
    public async Task Resolving_As_Duplicate_Without_A_Link_Is_A_409_And_With_One_Succeeds()
    {
        var (a, _) = await CreateTicketAsync();
        var (_, refB) = await CreateTicketAsync();

        (await ResolveAsync(a, "Duplicate")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await LinkAsync(a, "DuplicateOf", refB)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ResolveAsync(a, "Duplicate")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("AC", "925.4")]
    public async Task A_Link_Can_Be_Removed_And_A_Missing_One_Is_A_404()
    {
        var (a, _) = await CreateTicketAsync();
        var (_, refB) = await CreateTicketAsync();
        (await LinkAsync(a, "RelatedTo", refB)).StatusCode.Should().Be(HttpStatusCode.OK);
        var linkId = (await DetailAsync(a)).Links.Single().Id;

        (await _supervisor.DeleteAsync($"/api/Tickets/{a}/links/{linkId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await DetailAsync(a)).Links.Should().BeEmpty();
        (await _supervisor.DeleteAsync($"/api/Tickets/{a}/links/{linkId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record LinkedDetail(
        Guid Id, string Reference, string Status, string RowVersion, IReadOnlyList<LinkRow> Links);

    private sealed record LinkRow(
        Guid Id, string LinkType, string Direction, Guid OtherTicketId, string OtherReference, string OtherSubject);
}
