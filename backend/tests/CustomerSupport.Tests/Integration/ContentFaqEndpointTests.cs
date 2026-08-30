using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Contents.Dtos;
using CustomerSupport.Domain;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>FEAT-11, AC-175..AC-177 — FAQ curation from published articles only.</summary>
public class ContentFaqEndpointTests : IAsyncLifetime
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
            title = $"FAQ fixture {Guid.NewGuid():N}",
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

    private async Task<Guid> CreatePublishedAsync()
    {
        var id = await CreateDraftAsync();
        (await _client.PostAsync($"/api/Contents/{id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        return id;
    }

    [Fact]
    [Trait("AC", "175")]
    public async Task AC175_MarkFaq_PublishedArticle_Succeeds()
    {
        var id = await CreatePublishedAsync();

        var response = await _client.PutAsJsonAsync($"/api/Contents/{id}/faq", new { isFaq = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("AC", "176")]
    public async Task AC176_MarkFaq_DraftArticle_Returns409()
    {
        var id = await CreateDraftAsync();

        var response = await _client.PutAsJsonAsync($"/api/Contents/{id}/faq", new { isFaq = true });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("AC", "177")]
    public async Task AC177_FaqEndpoint_ReturnsOnlyFaqArticles()
    {
        var faqId = await CreatePublishedAsync();
        (await _client.PutAsJsonAsync($"/api/Contents/{faqId}/faq", new { isFaq = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await CreatePublishedAsync(); // not marked FAQ

        using var document = JsonDocument.Parse(await _client.GetStringAsync("/api/knowledge-base/articles/faq"));
        document.RootElement.GetProperty("data").GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should().Contain(faqId);
    }

    [Fact]
    [Trait("AC", "177")]
    public async Task AC177_UnmarkFaq_RemovesFromFaqEndpoint()
    {
        var id = await CreatePublishedAsync();
        await _client.PutAsJsonAsync($"/api/Contents/{id}/faq", new { isFaq = true });

        (await _client.PutAsJsonAsync($"/api/Contents/{id}/faq", new { isFaq = false }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await _client.GetStringAsync("/api/knowledge-base/articles/faq"));
        document.RootElement.GetProperty("data").GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should().NotContain(id);
    }
}
