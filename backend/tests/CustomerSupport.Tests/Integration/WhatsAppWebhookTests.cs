using System.Net;
using System.Security.Cryptography;
using System.Text;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Common;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-24 — Meta's WhatsApp webhook against the real external host. CC-8 (a signed payload runs
/// the shared ingestion path with Channel=WhatsApp), CC-9 (a retried message id records only one
/// row) and CC-5 (an unsigned or wrong-signed delivery is refused before any database write).
/// </summary>
public class WhatsAppWebhookTests : IAsyncLifetime
{
    private readonly CrmExternalApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        await _factory.SeedWhatsAppGatewayAsync("http://sandbox.whatsapp.test/v19.0/messages");
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync().AsTask();
    }

    private static string Sign(string secret, byte[] raw) =>
        "sha256=" + Convert.ToHexString(
            new HMACSHA256(Encoding.UTF8.GetBytes(secret)).ComputeHash(raw)).ToLowerInvariant();

    private static string MetaPayload(string from, string messageId, string body, string name = "Nadia Farouk") =>
        $$"""
        {
          "object": "whatsapp_business_account",
          "entry": [
            {
              "id": "WHATSAPP_BUSINESS_ACCOUNT_ID",
              "changes": [
                {
                  "value": {
                    "messaging_product": "whatsapp",
                    "metadata": { "display_phone_number": "15551234567", "phone_number_id": "PHONE_NUMBER_ID" },
                    "contacts": [ { "profile": { "name": "{{name}}" }, "wa_id": "{{from}}" } ],
                    "messages": [
                      {
                        "from": "{{from}}",
                        "id": "{{messageId}}",
                        "timestamp": "1720000000",
                        "type": "text",
                        "text": { "body": "{{body}}" }
                      }
                    ]
                  },
                  "field": "messages"
                }
              ]
            }
          ]
        }
        """;

    private Task<HttpResponseMessage> PostWebhookAsync(string json, string? signature)
    {
        // Not disposed here: SendAsync still owns the request's content stream while the body is
        // copied into the TestServer pipeline, and disposing it mid-flight throws exactly the
        // ObjectDisposedException this helper originally produced.
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/channels/whatsapp/webhook")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        if (signature is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Hub-Signature-256", signature);
        }

        return _client.SendAsync(request);
    }

    private async Task<List<Domain.Entities.Tickets.TicketMessage>> WhatsappMessagesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TicketMessages.Where(m => m.Channel == "WhatsApp").ToListAsync();
    }

    [Fact]
    [Trait("AC", "CC8")]
    public async Task CC8_SignedWebhook_RunsSharedIngestionAsWhatsAppTicket()
    {
        const string from = "15559998888";
        const string messageId = "wamid.HBgNcmVwcm9yLXRlc3Q=";
        var json = MetaPayload(from, messageId, "Can you help me with my bill?", name: "Nadia Farouk");
        var signature = Sign(GatewayTestData.WhatsAppAppSecret, Encoding.UTF8.GetBytes(json));

        var response = await PostWebhookAsync(json, signature);

        var rawBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"body: {rawBody.Substring(0, Math.Min(500, rawBody.Length))}");

        var messages = await WhatsappMessagesAsync();
        messages.Should().ContainSingle(m => m.ProviderMessageId == messageId);
        var stored = messages.Single(m => m.ProviderMessageId == messageId);
        stored.Direction.Should().Be("Inbound");
        stored.SenderId.Should().Be(SystemActors.ChannelIngestion);
        stored.Body.Should().Be("Can you help me with my bill?");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await db.Tickets.SingleAsync(t => t.Id == stored.TicketId);
        ticket.Source.Should().Be("WhatsApp");
        var customer = await db.Customers.SingleAsync(c => c.Id == ticket.CustomerId);
        customer.Phone.Should().Be(from);
        customer.Name.Should().Be("Nadia Farouk");
    }

    [Fact]
    [Trait("AC", "CC9")]
    public async Task CC9_RetriedDeliveryWithSameMessageId_StoresExactlyOneMessage()
    {
        const string from = "15551112222";
        const string messageId = "wamid.HBgNZHVwbGljYXRl";
        var json = MetaPayload(from, messageId, "Hello again");
        var signature = Sign(GatewayTestData.WhatsAppAppSecret, Encoding.UTF8.GetBytes(json));

        var first = await PostWebhookAsync(json, signature);
        var second = await PostWebhookAsync(json, signature);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var messages = await WhatsappMessagesAsync();
        messages.Where(m => m.ProviderMessageId == messageId).Should().HaveCount(1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticketId = messages.Single(m => m.ProviderMessageId == messageId).TicketId;
        var ticketCount = await db.Tickets.CountAsync(t => t.Id == ticketId && t.Source == "WhatsApp");
        ticketCount.Should().Be(1);
    }

    [Fact]
    [Trait("AC", "CC5")]
    public async Task CC5_UnsignedWebhook_RefusedBeforeAnyDatabaseWrite()
    {
        var json = MetaPayload("15550001111", "wamid.HBgNdW5zaWduZWQ=", "Forged");

        var response = await PostWebhookAsync(json, signature: null);

        var rawBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"body: {(rawBody.Length > 500 ? rawBody.Substring(0, 500) : rawBody)}");
        (await WhatsappMessagesAsync())
            .Should().NotContain(m => m.ProviderMessageId == "wamid.HBgNdW5zaWduZWQ=");
    }

    [Theory]
    [Trait("AC", "CC5")]
    [InlineData("the-wrong-app-secret")]
    [InlineData("sha256=deadbeef")]
    public async Task CC5_WrongSignature_RefusedBeforeAnyDatabaseWrite(string bogusSignature)
    {
        var json = MetaPayload("15550002222", "wamid.HBgNd3Jvbmctc2ln", "Forged with a bad key");
        var signature = bogusSignature != "sha256=deadbeef"
            ? Sign(bogusSignature, Encoding.UTF8.GetBytes(json))
            : bogusSignature;

        var response = await PostWebhookAsync(json, signature);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await WhatsappMessagesAsync())
            .Should().NotContain(m => m.ProviderMessageId == "wamid.HBgNd3Jvbmctc2ln");
    }
}
