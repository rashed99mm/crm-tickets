using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Messages;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// AI-38..AI-45 — the multi-turn chatbot surfaces, proven first in the deployment state every
/// test run has: no provider configured. Degraded mode must degrade (A2), and the portal host
/// must refuse anonymous callers before anything is persisted (AI-44).
/// </summary>
public class AiChatEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();

    public async Task InitializeAsync() => await _factory.EnsureDatabaseAsync();

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Start_WithoutProvider_ReturnsNotConfigured() // A2, AI-38
    {
        var (client, _) = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/ai/chats", new { message = "How do I reset my password?" });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Code.Should().Be(SystemCode.ERR052);
    }

    [Fact]
    public async Task Start_Unauthenticated_Returns401() // AI-41's staff analogue
    {
        var anon = _factory.CreateClient();

        var response = await anon.PostAsJsonAsync("/api/ai/chats", new { message = "hello there" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

/// <summary>AI-44 — the portal host refuses anonymous chat callers.</summary>
public class PortalAiChatEndpointTests : IAsyncLifetime
{
    private readonly CrmExternalApiFactory _factory = new();

    public async Task InitializeAsync() => await _factory.EnsureDatabaseAsync();

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Start_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ai/chats", new { message = "I need help" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
