using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>US-923 — matrix-only priority on the wire (AC-923.1/2/3/5/6).</summary>
public class TicketClassificationEndpointTests : IAsyncLifetime
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
            email = $"classification-{Guid.NewGuid():N}@example.com",
        });
        _customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    public Task DisposeAsync()
    {
        _supervisor.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private object CreatePayload(string impact = "High", string urgency = "High") => new
    {
        subject = "Cannot sign in",
        description = "The portal rejects my password.",
        customerId = _customerId,
        categoryId = _categoryId,
        impact,
        urgency,
    };

    [Fact]
    [Trait("AC", "923.1")]
    public async Task Create_Without_Impact_And_Urgency_Is_A_400_Naming_Both_Fields()
    {
        var response = await _supervisor.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Cannot sign in",
            description = "The portal rejects my password.",
            customerId = _customerId,
            categoryId = _categoryId,
            impact = "",
            urgency = "",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        body!.Errors.Should().Contain(e => e.Field == "Impact");
        body.Errors.Should().Contain(e => e.Field == "Urgency");
    }

    [Fact]
    [Trait("AC", "923.1")]
    [Trait("AC", "923.6")]
    public async Task Create_Derives_Priority_And_The_Detail_Carries_The_Inputs()
    {
        var created = await _supervisor.PostAsJsonAsync("/api/Tickets", CreatePayload("High", "High"));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
        var detail = await _supervisor.GetFromJsonAsync<Response<ClassifiedDetail>>($"/api/Tickets/{id}");
        detail!.Data!.Priority.Should().Be("Urgent");
        detail.Data.Impact.Should().Be("High");
        detail.Data.Urgency.Should().Be("High");
    }

    [Fact]
    [Trait("AC", "923.3")]
    public async Task A_Priority_Field_In_The_Body_Has_No_Effect()
    {
        var created = await _supervisor.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Cannot sign in",
            description = "The portal rejects my password.",
            customerId = _customerId,
            categoryId = _categoryId,
            impact = "Low",
            urgency = "Low",
            priority = "Urgent", // must be inert — the contract no longer has it
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
        var detail = await _supervisor.GetFromJsonAsync<Response<ClassifiedDetail>>($"/api/Tickets/{id}");
        detail!.Data!.Priority.Should().Be("Low");
    }

    [Fact]
    [Trait("AC", "923.2")]
    public async Task Reclassify_Rederives_And_Writes_A_Reprioritized_History_Row()
    {
        var created = await _supervisor.PostAsJsonAsync("/api/Tickets", CreatePayload("Medium", "Medium"));
        var id = (await created.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
        var before = await _supervisor.GetFromJsonAsync<Response<ClassifiedDetail>>($"/api/Tickets/{id}");

        var response = await _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/classification",
            new { impact = "High", urgency = "High", rowVersion = before!.Data!.RowVersion });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await _supervisor.GetFromJsonAsync<Response<ClassifiedDetail>>($"/api/Tickets/{id}");
        after!.Data!.Priority.Should().Be("Urgent");
        after.Data.History.Should().Contain(h =>
            h.ChangeType == "Reprioritized" && h.FromValue == "Normal" && h.ToValue == "Urgent");
    }

    [Fact]
    [Trait("AC", "923.1")]
    public async Task Reclassify_With_An_Unknown_Impact_Is_A_400()
    {
        var created = await _supervisor.PostAsJsonAsync("/api/Tickets", CreatePayload());
        var id = (await created.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
        var before = await _supervisor.GetFromJsonAsync<Response<ClassifiedDetail>>($"/api/Tickets/{id}");

        var response = await _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/classification",
            new { impact = "Critical", urgency = "High", rowVersion = before!.Data!.RowVersion });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        body!.Errors.Should().Contain(e => e.Field == "Impact");
    }

    private sealed record ClassifiedDetail(
        Guid Id, string Priority, string? Impact, string? Urgency, string RowVersion,
        IReadOnlyList<HistoryRow> History);

    private sealed record HistoryRow(string ChangeType, string? FromValue, string? ToValue);
}
