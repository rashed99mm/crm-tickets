using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-14 — recording and reading messages against a ticket. `AC-101` through `AC-109`.
/// Real LocalDB, same reasoning as every other endpoint suite here: FK constraints and ordering
/// criteria are not provable against the in-memory provider.
/// </summary>
public class TicketMessagesEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _callerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        var (client, caller) = await _factory.CreateAuthenticatedClientAsync();
        _client = client;
        _callerId = caller.Id;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    /// <summary>A ticket of this test's own — creates its own customer and category first.</summary>
    private async Task<Guid> CreateTicketAsync()
    {
        var customer = await _client.PostAsJsonAsync("/api/Customers", new
        {
            name = "Nadia Farouk",
            email = $"messages-{Guid.NewGuid():N}@example.com",
            phone = (string?)null,
        });
        var customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var categories = await _client.GetFromJsonAsync<Response<List<CategoryRow>>>("/api/Categories");
        var categoryId = categories!.Data!.First().Id;

        var ticket = await _client.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Cannot log in",
            description = "Password reset link never arrives.",
            customerId,
            categoryId,
            impact = "Medium",
            urgency = "Medium",
        });

        return (await ticket.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    private Task<HttpResponseMessage> RecordMessageAsync(Guid ticketId, object body) =>
        _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/messages", body);

    private async Task<List<TicketMessageRow>> GetMessagesAsync(Guid ticketId)
    {
        var response = await _client.GetFromJsonAsync<Response<List<TicketMessageRow>>>(
            $"/api/Tickets/{ticketId}/messages");
        return response!.Data!;
    }

    // --- AC-101 — recording a message ----------------------------------------------------------

    [Fact]
    [Trait("AC", "101")]
    public async Task AC101_RecordMessage_ValidFields_Returns201AndIsReadable()
    {
        var ticketId = await CreateTicketAsync();

        var response = await RecordMessageAsync(ticketId, new
        {
            direction = "Outbound",
            channel = "System",
            subject = "Follow-up",
            body = "Called the customer back.",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        created!.Data.Should().NotBeEmpty();

        var messages = await GetMessagesAsync(ticketId);
        var stored = messages.Single();
        stored.Id.Should().Be(created.Data);
        stored.Direction.Should().Be("Outbound");
        stored.Channel.Should().Be("System");
        stored.Subject.Should().Be("Follow-up");
        stored.Body.Should().Be("Called the customer back.");
        stored.SenderId.Should().Be(_callerId);
    }

    [Fact]
    [Trait("AC", "101")]
    public async Task AC101_RecordMessage_NoSubject_IsAllowed()
    {
        var ticketId = await CreateTicketAsync();

        var response = await RecordMessageAsync(ticketId, new
        {
            direction = "Inbound",
            channel = "Email",
            body = "Customer emailed to say the issue is resolved.",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetMessagesAsync(ticketId)).Single().Subject.Should().BeNull();
    }

    // --- AC-102 — empty body ---------------------------------------------------------------------

    [Fact]
    [Trait("AC", "102")]
    public async Task AC102_RecordMessage_EmptyBody_Returns400KeyedToBody()
    {
        var ticketId = await CreateTicketAsync();

        var response = await RecordMessageAsync(ticketId, new { direction = "Outbound", channel = "System", body = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == "Body");
        (await GetMessagesAsync(ticketId)).Should().BeEmpty();
    }

    // --- AC-103 — unknown ticket ------------------------------------------------------------------

    [Fact]
    [Trait("AC", "103")]
    public async Task AC103_RecordMessage_UnknownTicket_Returns404()
    {
        var response = await RecordMessageAsync(Guid.NewGuid(), new { direction = "Outbound", channel = "System", body = "Nobody's ticket." });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- AC-104 — invalid Direction/Channel ---------------------------------------------------------

    [Theory]
    [Trait("AC", "104")]
    [InlineData("Sideways", "System", "Direction")]
    [InlineData("Outbound", "Carrier Pigeon", "Channel")]
    public async Task AC104_RecordMessage_InvalidDirectionOrChannel_Returns400KeyedToField(
        string direction, string channel, string expectedField)
    {
        var ticketId = await CreateTicketAsync();

        var response = await RecordMessageAsync(ticketId, new { direction, channel, body = "Body text." });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == expectedField);
    }

    // --- AC-105 — authentication ----------------------------------------------------------------

    [Fact]
    [Trait("AC", "105")]
    public async Task AC105_RecordMessage_WithoutAToken_Returns401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            $"/api/Tickets/{Guid.NewGuid()}/messages",
            new { direction = "Outbound", channel = "System", body = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("AC", "105")]
    public async Task AC105_GetMessages_WithoutAToken_Returns401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/Tickets/{Guid.NewGuid()}/messages");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- AC-106 — ordering and sender names --------------------------------------------------------

    [Fact]
    [Trait("AC", "106")]
    public async Task AC106_GetMessages_ReturnsOldestFirstWithSenderNames()
    {
        var ticketId = await CreateTicketAsync();

        (await RecordMessageAsync(ticketId, new { direction = "Outbound", channel = "System", body = "First." }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // A real gap, or both SentAt stamps can land inside one tick and the order assertion below
        // proves nothing — the same reasoning CustomerNotesEndpointTests uses.
        await Task.Delay(20);

        (await RecordMessageAsync(ticketId, new { direction = "Inbound", channel = "Email", body = "Second." }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var messages = await GetMessagesAsync(ticketId);

        messages.Should().HaveCount(2);
        messages.Select(m => m.Body).Should().ContainInOrder("First.", "Second.");
        messages.Should().BeInAscendingOrder(m => m.SentAt);
        messages.Should().OnlyContain(m => m.SenderName == "Test User");
        messages.Should().OnlyContain(m => m.SenderId != Guid.Empty);
    }

    // --- AC-107 — unknown ticket ------------------------------------------------------------------

    [Fact]
    [Trait("AC", "107")]
    public async Task AC107_GetMessages_UnknownTicket_Returns404()
    {
        var response = await _client.GetAsync($"/api/Tickets/{Guid.NewGuid()}/messages");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- AC-108 — empty timeline is 200, not 404 -----------------------------------------------------

    [Fact]
    [Trait("AC", "108")]
    public async Task AC108_GetMessages_NoMessagesYet_ReturnsEmptyListNot404()
    {
        var ticketId = await CreateTicketAsync();

        var response = await _client.GetAsync($"/api/Tickets/{ticketId}/messages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetMessagesAsync(ticketId)).Should().BeEmpty();
    }

    // --- AC-109 — append-only, same proof as TicketHistory (AC-49) --------------------------------

    [Fact]
    [Trait("AC", "109")]
    public async Task AC109_UpdatingAMessageRow_IsRefused()
    {
        var ticketId = await CreateTicketAsync();
        (await RecordMessageAsync(ticketId, new { direction = "Outbound", channel = "System", body = "Original." }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.TicketMessages.FirstAsync(m => m.TicketId == ticketId);

        db.Entry(row).Property(m => m.Body).CurrentValue = "Tampered";

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
    }

    [Fact]
    [Trait("AC", "109")]
    public async Task AC109_DeletingAMessageRow_IsRefused()
    {
        var ticketId = await CreateTicketAsync();
        (await RecordMessageAsync(ticketId, new { direction = "Outbound", channel = "System", body = "Original." }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.TicketMessages.FirstAsync(m => m.TicketId == ticketId);

        db.TicketMessages.Remove(row);

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
    }

    public sealed record CategoryRow(Guid Id, string Name);

    public sealed record TicketMessageRow(
        Guid Id, string Direction, string Channel, string? Subject, string Body,
        Guid SenderId, string SenderName, DateTime SentAt);
}
