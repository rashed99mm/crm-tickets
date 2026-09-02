using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>US-924 — tags on the wire (AC-924.1/2/3/4).</summary>
public class TicketTagEndpointTests : IAsyncLifetime
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
            email = $"tags-{Guid.NewGuid():N}@example.com",
        });
        _customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    public Task DisposeAsync()
    {
        _supervisor.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> CreateTicketAsync()
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
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    private Task<HttpResponseMessage> AddTagAsync(Guid id, string value) =>
        _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/tags", new { value });

    private async Task<TaggedDetail> DetailAsync(Guid id) =>
        (await _supervisor.GetFromJsonAsync<Response<TaggedDetail>>($"/api/Tickets/{id}"))!.Data!;

    [Fact]
    [Trait("AC", "924.1")]
    [Trait("AC", "924.3")]
    public async Task Adding_A_Tag_Normalizes_Lists_And_Records_History()
    {
        var id = await CreateTicketAsync();

        var response = await AddTagAsync(id, "  Billing ISSUE ");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await DetailAsync(id);
        detail.Tags.Should().ContainSingle().Which.Should().Be("billing issue");
        detail.History.Should().Contain(h => h.ChangeType == "TagAdded" && h.ToValue == "billing issue");
    }

    [Fact]
    [Trait("AC", "924.2")]
    public async Task An_Arabic_Tag_Round_Trips_Intact()
    {
        var id = await CreateTicketAsync();

        (await AddTagAsync(id, "فوترة")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await DetailAsync(id)).Tags.Should().Contain("فوترة");
    }

    [Fact]
    [Trait("AC", "924.1")]
    public async Task A_Duplicate_Tag_Is_A_400_On_The_Value_Field()
    {
        var id = await CreateTicketAsync();
        (await AddTagAsync(id, "billing")).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await AddTagAsync(id, " BILLING ");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        body!.Errors.Should().Contain(e => e.Field == "Value");
    }

    [Fact]
    [Trait("AC", "924.1")]
    public async Task The_Eleventh_Tag_Is_A_400()
    {
        var id = await CreateTicketAsync();
        for (var i = 1; i <= 10; i++)
        {
            (await AddTagAsync(id, $"tag-{i}")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var response = await AddTagAsync(id, "tag-11");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("AC", "924.1")]
    [Trait("AC", "924.3")]
    public async Task Removing_A_Tag_Deletes_It_And_Records_History()
    {
        var id = await CreateTicketAsync();
        (await AddTagAsync(id, "billing")).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _supervisor.DeleteAsync($"/api/Tickets/{id}/tags/billing");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await DetailAsync(id);
        detail.Tags.Should().BeEmpty();
        detail.History.Should().Contain(h => h.ChangeType == "TagRemoved" && h.FromValue == "billing");
    }

    [Fact]
    [Trait("AC", "924.1")]
    public async Task Removing_A_Missing_Tag_Is_A_404()
    {
        var id = await CreateTicketAsync();

        var response = await _supervisor.DeleteAsync($"/api/Tickets/{id}/tags/nothing");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("AC", "924.4")]
    public async Task The_Queue_Filters_By_Tag_Server_Side()
    {
        var tagged = await CreateTicketAsync();
        var untagged = await CreateTicketAsync();
        var marker = $"queue-{Guid.NewGuid():N}"[..20];
        (await AddTagAsync(tagged, marker)).StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await _supervisor.GetFromJsonAsync<Response<PagedTickets>>($"/api/Tickets?tag={marker}");

        page!.Data!.Items.Should().ContainSingle(t => t.Id == tagged);
        page.Data.Items.Should().NotContain(t => t.Id == untagged);
    }

    private sealed record TaggedDetail(Guid Id, IReadOnlyList<string> Tags, IReadOnlyList<HistoryRow> History);
    private sealed record HistoryRow(string ChangeType, string? FromValue, string? ToValue);
    private sealed record PagedTickets(IReadOnlyList<Row> Items);
    private sealed record Row(Guid Id, IReadOnlyList<string> Tags);
}
