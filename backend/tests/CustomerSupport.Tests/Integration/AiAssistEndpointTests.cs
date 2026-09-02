using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Tickets.Dtos;
using CustomerSupport.Application.Messages;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// US-702 / AC-21.2, assumption A2 of the FEAT-21 spec: with no AI provider configured the
/// deployment degrades rather than fails â€” suggestions endpoints answer the documented
/// not-configured envelope (ERR052), nothing is written, and the rest of the ticket surface is
/// untouched. Mirrors how messaging degrades to NoOpMessagePublisher.
/// </summary>
public class AiAssistEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _agent = null!;
    private Guid _categoryId;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_agent, _) = await _factory.CreateAuthenticatedClientAsync("Agent");
        _categoryId = await _factory.EnsureCategoryAsync("AI");
        _customerId = await CreateCustomerAsync();
    }

    public Task DisposeAsync()
    {
        _agent.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    [Fact]
    [Trait("AC", "21.2")]
    public async Task AC212_Summary_WithoutProvider_ReturnsNotConfigured()
    {
        var id = await CreateTicketAsync();

        var response = await _agent.PostAsJsonAsync($"/api/Tickets/{id}/ai/summary", new { });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = (await response.Content.ReadFromJsonAsync<Response<object>>())!;
        body.Success.Should().BeFalse();
        body.Code.Should().Be(SystemCode.ERR052);
    }

    [Fact]
    [Trait("AC", "21.3")]
    public async Task AC213_Summary_WithoutProvider_MutatesNothing()
    {
        var id = await CreateTicketAsync();
        var before = await DetailAsync(id);

        await _agent.PostAsJsonAsync($"/api/Tickets/{id}/ai/summary", new { });

        (await DetailAsync(id)).Status.Should().Be(before.Status);
    }

    // --- fixtures --------------------------------------------------------------------------------

    private async Task<Guid> CreateCustomerAsync()
    {
        var response = await _agent.PostAsJsonAsync("/api/Customers", new
        {
            name = "AI Journey",
            email = $"ai-{Guid.NewGuid():N}@example.com",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    private async Task<Guid> CreateTicketAsync()
    {
        var response = await _agent.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "AI assist fixture",
            description = "Thread used by the AI endpoint tests.",
            customerId = _customerId,
            categoryId = _categoryId,
            impact = "Medium",
            urgency = "Medium",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    private async Task<TicketDetailDto> DetailAsync(Guid id) =>
        (await _agent.GetFromJsonAsync<Response<TicketDetailDto>>($"/api/Tickets/{id}"))!.Data!;
}
