using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CustomerSupport.Application.Contracts;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>FEAT-11, AC-168..AC-170 — every save produces a new version record.</summary>
public class ContentVersioningEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _client = null!;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
            title = $"Versioning fixture {Guid.NewGuid():N}",
            body = "Body text.",
            contentType = "Article",
            authorId = Guid.NewGuid(),
            status = "Draft",
            tags = Array.Empty<string>(),
        });
        var bodyText = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, bodyText);
        return JsonSerializer.Deserialize<Response<Guid>>(bodyText, JsonOptions)!.Data;
    }

    [Fact]
    [Trait("AC", "168")]
    public async Task AC168_SavingChange_IncrementsVersion()
    {
        var id = await CreateDraftAsync(); // version 1 per AC-169

        await _client.PutAsJsonAsync($"/api/Contents/{id}", new { title = "Updated title" });

        var detail = await _client.GetFromJsonAsync<Response<ContentRow>>($"/api/Contents/{id}");
        detail!.Data!.Version.Should().Be(2);
    }

    [Fact]
    [Trait("AC", "169")]
    public async Task AC169_NewArticle_StartsAtVersion1()
    {
        var id = await CreateDraftAsync();

        var detail = await _client.GetFromJsonAsync<Response<ContentRow>>($"/api/Contents/{id}");

        detail!.Data!.Version.Should().Be(1);
    }

    [Fact]
    [Trait("AC", "170")]
    public async Task AC170_VersionHistory_ReturnsNewestFirstWithMetadata()
    {
        var id = await CreateDraftAsync();
        await _client.PutAsJsonAsync($"/api/Contents/{id}", new { title = "First edit" });
        await _client.PutAsJsonAsync($"/api/Contents/{id}", new { title = "Second edit" });

        var versions = await _client.GetFromJsonAsync<Response<List<VersionRow>>>($"/api/Contents/{id}/versions");

        versions!.Data.Should().HaveCount(3); // 1 (create) + 2 edits
        versions.Data![0].VersionNumber.Should().Be(3);
        versions.Data[0].ChangeSummary.Should().NotBeNullOrEmpty();
    }

    public sealed record VersionRow(int VersionNumber, Guid AuthorId, string ChangeSummary, DateTime CreatedAt);
    public sealed record ContentRow(string Status, DateTime? PublishedAt, int Version);
}
