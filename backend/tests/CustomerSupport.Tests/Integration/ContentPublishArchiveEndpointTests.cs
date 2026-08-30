using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>FEAT-11, AC-165..AC-167 — dedicated publish/archive commands over the existing
/// `Content.Publish()`/`Archive()` domain methods.</summary>
public class ContentPublishArchiveEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_client, _) = await _factory.CreateAuthenticatedClientAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> CreateDraftAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/Contents", new
        {
            title = $"Publish fixture {Guid.NewGuid():N}",
            body = "Body text.",
            contentType = "Article",
            authorId = Guid.NewGuid(), // validated as non-empty but ignored server-side — the
                                        // controller always uses the session's own user id.
            status = "Draft",
            tags = Array.Empty<string>(),
        });
        var bodyText = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, bodyText);
        return System.Text.Json.JsonSerializer.Deserialize<Response<Guid>>(bodyText, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!.Data;
    }

    [Fact]
    [Trait("AC", "165")]
    public async Task AC165_Publish_FromDraft_SetsStatusPublished()
    {
        var id = await CreateDraftAsync();

        var response = await _client.PostAsync($"/api/Contents/{id}/publish", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await _client.GetFromJsonAsync<Response<ContentRow>>($"/api/Contents/{id}");
        detail!.Data!.Status.Should().Be("Published");
        detail.Data.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("AC", "167")]
    public async Task AC167_Publish_FromArchived_Returns409()
    {
        var id = await CreateDraftAsync();
        (await _client.PostAsync($"/api/Contents/{id}/archive", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.PostAsync($"/api/Contents/{id}/publish", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("AC", "166")]
    public async Task AC166_Archive_FromDraft_SetsStatusArchived()
    {
        var id = await CreateDraftAsync();

        var response = await _client.PostAsync($"/api/Contents/{id}/archive", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await _client.GetFromJsonAsync<Response<ContentRow>>($"/api/Contents/{id}");
        detail!.Data!.Status.Should().Be("Archived");
    }

    public sealed record ContentRow(string Status, DateTime? PublishedAt, int Version);
}
