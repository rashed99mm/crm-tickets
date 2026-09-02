using System.Net;
using System.Security.Cryptography;
using System.Text;
using CustomerSupport.Domain.Common;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-25 — Twilio's inbound SMS webhook against the real external host. CC-40 (a validly-signed
/// delivery runs the shared ingestion path with Channel=SMS) and CC-41 (an unsigned or wrongly
/// signed delivery is refused before any database write).
/// </summary>
public class SmsInboundWebhookTests : IAsyncLifetime
{
    private const string Path = "/api/channels/sms/webhook";
    private readonly CrmExternalApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        await _factory.SeedSmsGatewayAsync("https://api.twilio.test/2010-04-01/Accounts/ACtest/Messages.json");
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync().AsTask();
    }

    /// <summary>Twilio's recipe: URL + ordinal-sorted key/value pairs, HMAC-SHA1, Base64.</summary>
    private static string Sign(string secret, string url, params (string Key, string Value)[] form)
    {
        var payload = new StringBuilder(url);
        foreach (var (key, value) in form.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            payload.Append(key).Append(value);
        }

        using var mac = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(mac.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString())));
    }

    private Task<HttpResponseMessage> PostAsync((string Key, string Value)[] form, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Path)
        {
            Content = new FormUrlEncodedContent(
                form.Select(f => new KeyValuePair<string, string>(f.Key, f.Value))),
        };

        if (signature is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Twilio-Signature", signature);
        }

        return _client.SendAsync(request);
    }

    /// <summary>The URL Twilio signs is the one it posts to — built from the client's own base.</summary>
    private string SignedUrl => $"{_client.BaseAddress}".TrimEnd('/') + Path;

    private async Task<List<Domain.Entities.Tickets.TicketMessage>> SmsMessagesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TicketMessages.Where(m => m.Channel == ChannelNames.Sms).ToListAsync();
    }

    [Fact]
    [Trait("AC", "CC40")]
    public async Task CC40_SignedWebhook_RunsSharedIngestionAsSmsTicket()
    {
        var form = new[]
        {
            ("From", "+15551230001"),
            ("Body", "My order has not arrived"),
            ("MessageSid", "SM40000000000000000000000000000001"),
        };
        var signature = Sign(GatewayTestData.SmsAuthToken, SignedUrl, form);

        var response = await PostAsync(form, signature);

        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"body: {raw[..Math.Min(500, raw.Length)]}");

        var stored = (await SmsMessagesAsync())
            .Should().ContainSingle(m => m.ProviderMessageId == "SM40000000000000000000000000000001").Subject;
        stored.Direction.Should().Be("Inbound");
        stored.SenderId.Should().Be(SystemActors.ChannelIngestion);
        stored.Body.Should().Be("My order has not arrived");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await db.Tickets.SingleAsync(t => t.Id == stored.TicketId);
        ticket.Source.Should().Be(ChannelNames.Sms);
        var customer = await db.Customers.SingleAsync(c => c.Id == ticket.CustomerId);
        customer.Phone.Should().Be("+15551230001");
    }

    [Fact]
    [Trait("AC", "CC40")]
    public async Task CC40_RetriedDeliveryWithSameMessageSid_StoresExactlyOneMessage()
    {
        var form = new[]
        {
            ("From", "+15551230002"),
            ("Body", "Still waiting"),
            ("MessageSid", "SM40000000000000000000000000000002"),
        };
        var signature = Sign(GatewayTestData.SmsAuthToken, SignedUrl, form);

        (await PostAsync(form, signature)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PostAsync(form, signature)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await SmsMessagesAsync())
            .Where(m => m.ProviderMessageId == "SM40000000000000000000000000000002")
            .Should().HaveCount(1);
    }

    [Fact]
    [Trait("AC", "CC41")]
    public async Task CC41_UnsignedWebhook_RefusedBeforeAnyDatabaseWrite()
    {
        var form = new[]
        {
            ("From", "+15551230003"),
            ("Body", "Forged"),
            ("MessageSid", "SM40000000000000000000000000000003"),
        };

        var response = await PostAsync(form, signature: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SmsMessagesAsync())
            .Should().NotContain(m => m.ProviderMessageId == "SM40000000000000000000000000000003");
    }

    [Fact]
    [Trait("AC", "CC41")]
    public async Task CC41_WrongSignature_RefusedBeforeAnyDatabaseWrite()
    {
        var form = new[]
        {
            ("From", "+15551230004"),
            ("Body", "Forged with a bad key"),
            ("MessageSid", "SM40000000000000000000000000000004"),
        };
        var signature = Sign("the-wrong-auth-token", SignedUrl, form);

        var response = await PostAsync(form, signature);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SmsMessagesAsync())
            .Should().NotContain(m => m.ProviderMessageId == "SM40000000000000000000000000000004");
    }

    [Fact]
    [Trait("AC", "CC40")]
    public async Task CC40_SignedDeliveryWithNoBody_IsRefusedAsUningestible()
    {
        // Authentic but empty: Twilio sends delivery-status callbacks to the same URL, and they
        // carry no Body. Answering 400 (not 500) keeps them out of the ingestion path.
        var form = new[] { ("From", "+15551230005"), ("MessageSid", "SM40000000000000000000000000000005") };
        var signature = Sign(GatewayTestData.SmsAuthToken, SignedUrl, form);

        var response = await PostAsync(form, signature);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SmsMessagesAsync())
            .Should().NotContain(m => m.ProviderMessageId == "SM40000000000000000000000000000005");
    }
}
