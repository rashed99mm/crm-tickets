using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-24 — the outbound reply path (CC-10). An agent replying to a WhatsApp ticket records an
/// outbound TicketMessage and dispatches the reply through INotificationGateway to the configured
/// WhatsApp gateway — a real, test-local stub over Kestrel, so the assertion is on real HTTP.
/// </summary>
public class WhatsAppOutboundReplyTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private StubGatewayServer _stub = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        _stub = await StubGatewayServer.StartAsync();
        await _factory.SeedWhatsAppGatewayAsync(_stub.BaseUrl);
        (_client, _) = await _factory.CreateAuthenticatedClientAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _stub.DisposeAsync();
        await _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> CreateWhatsAppTicketAsync(string phone = "15559998888")
    {
        var customer = await _client.PostAsJsonAsync("/api/Customers", new
        {
            name = "Nadia Farouk",
            email = $"whatsapp-reply-{Guid.NewGuid():N}@example.com",
            phone,
        });
        var customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var categories = await _client.GetFromJsonAsync<Response<List<CategoryRow>>>("/api/Categories");
        var categoryId = categories!.Data!.First().Id;

        var ticket = await _client.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Billing question",
            description = "Bill looks wrong.",
            customerId,
            categoryId,
            priority = "Normal",
        });

        return (await ticket.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    private Task<HttpResponseMessage> ReplyAsync(Guid ticketId, string channel, string body) =>
        _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/messages", new
        {
            direction = "Outbound",
            channel,
            body,
        });

    [Fact]
    [Trait("AC", "CC10")]
    public async Task CC10_WhatsAppReply_RecordsOutboundMessageAndDispatchesToTheGateway()
    {
        var ticketId = await CreateWhatsAppTicketAsync();

        var response = await ReplyAsync(ticketId, "WhatsApp", "Your bill is settled.");

        var rawBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, $"body: {(rawBody.Length > 500 ? rawBody.Substring(0, 500) : rawBody)}");

        using (var diag = _factory.Services.CreateScope())
        {
            var configRow = diag.ServiceProvider.GetRequiredService<AppDbContext>()
                .Set<CustomerSupport.Domain.Entities.ExternalApis.ExternalApiConfiguration>()
                .SingleOrDefault(c => c.Name == "WhatsAppGateway");
            configRow.Should().NotBeNull();
            var provider = diag.ServiceProvider.GetRequiredService<CustomerSupport.Application.Interfaces.IExternalApiConfigurationProvider>();
            var cfg = provider.GetConfig(NotificationGatewayConstants.WhatsAppGatewayConfigName);
            cfg.Should().NotBeNull($"provider reload must surface the seeded {configRow?.BaseUrl}");
        }

        _stub.ReceivedBodies.Should().HaveCount(1);
        using var json = JsonDocument.Parse(_stub.ReceivedBodies.Single());
        json.RootElement.GetProperty("messaging_product").GetString().Should().Be("whatsapp");
        json.RootElement.GetProperty("to").GetString().Should().Be("15559998888");
        json.RootElement.GetProperty("text").GetProperty("body").GetString().Should().Be("Your bill is settled.");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.TicketMessages.SingleAsync(
            m => m.TicketId == ticketId && m.Channel == "WhatsApp" && m.Direction == "Outbound");
        stored.Body.Should().Be("Your bill is settled.");
    }

    [Fact]
    [Trait("AC", "CC10")]
    public async Task CC10_SystemReply_DoesNotCallTheGateway()
    {
        var ticketId = await CreateWhatsAppTicketAsync();

        var response = await ReplyAsync(ticketId, "System", "Logged internally.");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _stub.ReceivedBodies.Should().BeEmpty();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.TicketMessages.SingleAsync(
            m => m.TicketId == ticketId && m.Channel == "System" && m.Direction == "Outbound");
        stored.Body.Should().Be("Logged internally.");
    }

    public sealed record CategoryRow(Guid Id, string Name);
}
