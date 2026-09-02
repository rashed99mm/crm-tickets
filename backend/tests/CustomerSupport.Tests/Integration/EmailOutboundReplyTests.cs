using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Common;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// CC-44 — an agent's reply on an email-sourced ticket leaves through INotificationGateway on the
/// Email channel, addressed to the customer's email. Asserted over real HTTP against
/// StubGatewayServer, the same way WhatsAppOutboundReplyTests asserts CC-10, because the defect
/// this covers (spec A27) is precisely that the wrong field reached the transport.
/// </summary>
public class EmailOutboundReplyTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private StubGatewayServer _stub = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        _stub = await StubGatewayServer.StartAsync();
        // The sender POSTs straight to config.BaseUrl; the stub's handler is mapped at /messages.
        await _factory.SeedEmailGatewayAsync($"{_stub.BaseUrl.TrimEnd('/')}/messages");
        (_client, _) = await _factory.CreateAuthenticatedClientAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _stub.DisposeAsync();
        await _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> CreateTicketAsync(string email, string? phone)
    {
        var customer = await _client.PostAsJsonAsync("/api/Customers", new
        {
            name = "Layla Haddad",
            email,
            phone,
        });
        var customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var categories = await _client.GetFromJsonAsync<Response<List<CategoryRow>>>("/api/Categories");
        var categoryId = categories!.Data!.First().Id;

        var ticket = await _client.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Refund query",
            description = "Where is my refund?",
            customerId,
            categoryId,
            impact = "Medium",
            urgency = "Medium",
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
    [Trait("AC", "CC44")]
    public async Task CC44_EmailReply_DispatchesToTheEmailGatewayAddressedToTheCustomer()
    {
        var email = $"cc44-{Guid.NewGuid():N}@example.com";
        var ticketId = await CreateTicketAsync(email, phone: null);

        var response = await ReplyAsync(ticketId, ChannelNames.Email, "Your refund is on its way.");

        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, $"body: {raw[..Math.Min(500, raw.Length)]}");

        _stub.ReceivedBodies.Should().HaveCount(1, "the email reply must actually leave the process");

        // SendGrid v3's shape, as plan 1's EmailNotificationChannelSender builds it.
        using var json = JsonDocument.Parse(_stub.ReceivedBodies.Single());
        json.RootElement
            .GetProperty("personalizations")[0].GetProperty("to")[0].GetProperty("email")
            .GetString().Should().Be(email, "A27 — the customer's address, not their phone number");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.TicketMessages.SingleAsync(
            m => m.TicketId == ticketId && m.Channel == ChannelNames.Email && m.Direction == "Outbound");
        stored.Body.Should().Be("Your refund is on its way.");
    }

    [Fact]
    [Trait("AC", "CC44")]
    public async Task CC44_PlaceholderChannelInvalidAddress_IsNotDispatched()
    {
        // The address IngestInboundChannelMessageCommandHandler mints for phone-only customers.
        // A27: recording a send to it would report success and deliver nothing.
        var ticketId = await CreateTicketAsync("15551230009@channel.invalid", phone: "15551230009");

        var response = await ReplyAsync(ticketId, ChannelNames.Email, "Should not be sent.");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _stub.ReceivedBodies.Should().BeEmpty();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.TicketMessages.SingleAsync(
            m => m.TicketId == ticketId && m.Channel == ChannelNames.Email && m.Direction == "Outbound");
        stored.Body.Should().Be("Should not be sent.", "the message is still recorded on the ticket");
    }

    [Fact]
    [Trait("AC", "CC44")]
    public async Task CC44_SystemReply_StillDoesNotCallAnyGateway()
    {
        var ticketId = await CreateTicketAsync($"cc44-system-{Guid.NewGuid():N}@example.com", phone: null);

        var response = await ReplyAsync(ticketId, ChannelNames.System, "Internal note.");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _stub.ReceivedBodies.Should().BeEmpty();
    }

    public sealed record CategoryRow(Guid Id, string Name);
}
