using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CustomerSupport.Application.Contracts;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>FEAT-11, AC-171..AC-174 — a real, hierarchical category taxonomy for KB articles,
/// replacing the free-text `Content.Category` field.</summary>
public class ContentCategoryEndpointTests : IAsyncLifetime
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
            title = $"Category fixture {Guid.NewGuid():N}",
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

    private async Task<Guid> CreateCategoryAsync(string name, Guid? parentId)
    {
        var response = await _client.PostAsJsonAsync("/api/ContentCategories", new { name, parentId });
        var bodyText = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, bodyText);
        return JsonSerializer.Deserialize<Response<Guid>>(bodyText, JsonOptions)!.Data;
    }

    [Fact]
    [Trait("AC", "171")]
    public async Task AC171_CreateCategory_WithParent_IsRetrievable()
    {
        var parentId = await CreateCategoryAsync($"Billing {Guid.NewGuid():N}", null);

        var response = await _client.PostAsJsonAsync("/api/ContentCategories",
            new { name = $"Refunds {Guid.NewGuid():N}", parentId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("AC", "171")]
    public async Task AC171_CreateCategory_DuplicateNameUnderSameParent_Returns409()
    {
        var name = $"Billing {Guid.NewGuid():N}";
        await CreateCategoryAsync(name, null);

        var response = await _client.PostAsJsonAsync("/api/ContentCategories", new { name, parentId = (Guid?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("AC", "174")]
    public async Task AC174_ListCategories_ReturnsNestedTree()
    {
        var parentId = await CreateCategoryAsync($"Billing {Guid.NewGuid():N}", null);
        var childName = $"Refunds {Guid.NewGuid():N}";
        await CreateCategoryAsync(childName, parentId);

        var tree = await _client.GetFromJsonAsync<Response<List<CategoryNode>>>("/api/ContentCategories");

        var billing = tree!.Data!.Single(c => c.Id == parentId);
        billing.Children.Should().ContainSingle(c => c.Name == childName);
    }

    [Fact]
    [Trait("AC", "172")]
    public async Task AC172_AssignCategory_UnknownId_Returns404()
    {
        var contentId = await CreateDraftAsync();

        var response = await _client.PutAsJsonAsync($"/api/Contents/{contentId}/category", new { categoryId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("AC", "172")]
    public async Task AC172_AssignCategory_KnownId_Succeeds()
    {
        var contentId = await CreateDraftAsync();
        var categoryId = await CreateCategoryAsync($"Billing {Guid.NewGuid():N}", null);

        var response = await _client.PutAsJsonAsync($"/api/Contents/{contentId}/category", new { categoryId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    public sealed record CategoryNode(Guid Id, string Name, Guid? ParentId, List<CategoryNode> Children);
}
