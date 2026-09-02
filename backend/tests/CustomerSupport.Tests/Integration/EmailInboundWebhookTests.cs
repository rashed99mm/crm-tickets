using System.Net;
using CustomerSupport.Domain.Common;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-35 — inbound email in SendGrid Inbound Parse's shape (CC-42), and its idempotency on a
/// repeated Message-ID (CC-43). No signature: Inbound Parse does not sign its posts (spec A21).
/// </summary>
public class EmailInboundWebhookTests : IAsyncLifetime
{
    private const string Path = "/api/channels/email/webhook";
    private readonly CrmExternalApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync().AsTask();
    }

    /// <summary>Inbound Parse posts multipart/form-data with these field names.</summary>
    private Task<HttpResponseMessage> PostAsync(
        string from, string subject, string text, string? messageId, string? envelope = null)
    {
        var headers = messageId is null
            ? "Received: by mx.example.com\r\nSubject: " + subject
            : $"Received: by mx.example.com\r\nMessage-ID: {messageId}\r\nSubject: {subject}";

        var content = new MultipartFormDataContent
        {
            { new StringContent(headers), "headers" },
            { new StringContent(from), "from" },
            { new StringContent("support@example.com"), "to" },
            { new StringContent(subject), "subject" },
            { new StringContent(text), "text" },
            {
                new StringContent(envelope
                    ?? $"{{\"to\":[\"support@example.com\"],\"from\":\"{from}\"}}"),
                "envelope"
            },
        };

        return _client.PostAsync(Path, content);
    }

    private async Task<List<Domain.Entities.Tickets.TicketMessage>> EmailMessagesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TicketMessages.Where(m => m.Channel == ChannelNames.Email).ToListAsync();
    }

    [Fact]
    [Trait("AC", "CC42")]
    public async Task CC42_InboundEmail_RunsSharedIngestionAsEmailTicket()
    {
        const string messageId = "<CC42.inbound.1@mail.example.com>";

        var response = await PostAsync(
            from: "\"Layla Haddad\" <layla.cc42@example.com>",
            subject: "Refund not received",
            text: "I was told the refund would arrive last week.",
            messageId: messageId);

        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"body: {raw[..Math.Min(500, raw.Length)]}");

        var stored = (await EmailMessagesAsync())
            .Should().ContainSingle(m => m.ProviderMessageId == messageId).Subject;
        stored.Direction.Should().Be("Inbound");
        stored.SenderId.Should().Be(SystemActors.ChannelIngestion);
        stored.Body.Should().Be("I was told the refund would arrive last week.");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await db.Tickets.SingleAsync(t => t.Id == stored.TicketId);
        ticket.Source.Should().Be(ChannelNames.Email);
        // A23: the email's own Subject: header, not the synthesized "Email — Name" default.
        ticket.Subject.Should().Be("Refund not received");

        // A17: matched/created by email address, with the display name parsed out of the From header.
        var customer = await db.Customers.SingleAsync(c => c.Id == ticket.CustomerId);
        customer.Email.Should().Be("layla.cc42@example.com");
        customer.Name.Should().Be("Layla Haddad");
    }

    [Fact]
    [Trait("AC", "CC43")]
    public async Task CC43_SameMessageIdTwice_StoresExactlyOneMessage()
    {
        const string messageId = "<CC43.duplicate@mail.example.com>";

        var first = await PostAsync("dup.cc43@example.com", "Duplicate", "Sent twice", messageId);
        var second = await PostAsync("dup.cc43@example.com", "Duplicate", "Sent twice", messageId);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        (await EmailMessagesAsync())
            .Where(m => m.ProviderMessageId == messageId).Should().HaveCount(1);
    }

    [Fact]
    [Trait("AC", "CC42")]
    public async Task CC42_BarePlainAddressWithNoDisplayName_IsAccepted()
    {
        const string messageId = "<CC42.bare@mail.example.com>";

        var response = await PostAsync("bare.cc42@example.com", "No display name", "Body here", messageId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Customers.AnyAsync(c => c.Email == "bare.cc42@example.com")).Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "CC42")]
    public async Task CC42_MissingMessageIdHeader_StillIngests()
    {
        var response = await PostAsync(
            "no.id.cc42@example.com", "No Message-ID", "Some senders omit it", messageId: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = await db.Customers.SingleAsync(c => c.Email == "no.id.cc42@example.com");
        (await db.Tickets.AnyAsync(t => t.CustomerId == customer.Id && t.Source == ChannelNames.Email))
            .Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "CC42")]
    public async Task CC42_EmptyBody_IsRefusedWithoutAWrite()
    {
        var response = await PostAsync(
            "empty.cc42@example.com", "Nothing inside", text: "   ", messageId: "<CC42.empty@x>");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Customers.AnyAsync(c => c.Email == "empty.cc42@example.com")).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC42")]
    public async Task CC42_UnparseableFromHeader_IsRefusedWithoutAWrite()
    {
        var response = await PostAsync(
            from: "not-an-address", subject: "Broken sender", text: "Body", messageId: "<CC42.bad@x>");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await EmailMessagesAsync()).Should().NotContain(m => m.ProviderMessageId == "<CC42.bad@x>");
    }
}
