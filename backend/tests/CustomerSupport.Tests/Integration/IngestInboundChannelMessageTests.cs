using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-24..27 — CC-1..CC-4. The shared inbound-channel ingestion command against real LocalDB.
///
/// No endpoint exists yet (the WhatsApp/SMS/web-form webhook controllers land in Tasks 2/3/5), so
/// this drives the command through the host's MediatR pipeline — which is exactly what those
/// controllers do once they parse their provider payload. The pipeline still runs the FluentValidation
/// behavior, so the "rejected before any write" criteria are exercised the same way an HTTP call
/// would.
/// </summary>
public class IngestInboundChannelMessageTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();

    public Task InitializeAsync() => _factory.EnsureDatabaseAsync();

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private async Task<Response<Guid>> SendAsync(IngestInboundChannelMessageCommand command, CancellationToken ct = default)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(command, ct);
    }

    private async Task<(int Customers, int Tickets, int Messages)> CountsAsync(string channel, string phone)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customerId = await db.Customers
            .Where(c => c.Phone == phone)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();

        var customers = customerId == Guid.Empty ? 0 : 1;
        var tickets = await db.Tickets.CountAsync(t => t.CustomerId == customerId);

        var ticketIds = await db.Tickets
            .Where(t => t.CustomerId == customerId)
            .Select(t => t.Id)
            .ToListAsync();
        var messages = await db.TicketMessages.CountAsync(
            m => ticketIds.Contains(m.TicketId) && m.Channel == channel);

        return (customers, tickets, messages);
    }

    private async Task<(int TotalCustomers, int TotalMessages)> TotalCountsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Customers.CountAsync(), await db.TicketMessages.CountAsync());
    }

    /// <summary>A unique phone, short enough (≤32) and E.164-ish so the placeholder email it ships
    /// as a customer's email satisfies the domain's format rule.</summary>
    private static string UniquePhone() => "+1555" + Guid.NewGuid().ToString("N")[..12];

    // --- CC-1/CC-3 — first contact creates customer and ticket ------------------------------------

    [Fact]
    public async Task CC1_CC3_FirstContact_CreatesCustomerAndNewTicket()
    {
        var phone = UniquePhone();

        var result = await SendAsync(new IngestInboundChannelMessageCommand(
            "WhatsApp", "Nadia", phone, null, "My card was charged twice.", "wa-1"));

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeEmpty();

        var (customers, tickets, messages) = await CountsAsync("WhatsApp", phone);
        customers.Should().Be(1);
        tickets.Should().Be(1);
        messages.Should().Be(1);
    }

    // --- CC-2 — a second message from the same customer/channel appends to the same open ticket ---

    [Fact]
    public async Task CC2_SecondMessage_AppendsToTheSameNonTerminalTicket()
    {
        var phone = UniquePhone();

        var first = await SendAsync(new IngestInboundChannelMessageCommand("WhatsApp", "Nadia", phone, null, "First.", "wa-2a"));
        var second = await SendAsync(new IngestInboundChannelMessageCommand("WhatsApp", "Nadia", phone, null, "Second.", "wa-2b"));

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customerId = await db.Customers.Where(c => c.Phone == phone).Select(c => c.Id).SingleAsync();
        var ticket = await db.Tickets.SingleAsync(t => t.CustomerId == customerId);
        var messages = await db.TicketMessages
            .Where(m => m.TicketId == ticket.Id)
            .OrderBy(m => m.SentAt)
            .Select(m => m.Body)
            .ToListAsync();

        ticket.Source.Should().Be("WhatsApp");
        messages.Should().Equal("First.", "Second.");
    }

    // --- CC-2 — a message after resolution starts a NEW ticket; terminal tickets are not reused ---

    [Fact]
    public async Task CC2_MessageAfterResolution_StartsANewTicket()
    {
        var phone = UniquePhone();

        await SendAsync(new IngestInboundChannelMessageCommand("WhatsApp", "Nadia", phone, null, "First.", "wa-3a"));
        await SendAsync(new IngestInboundChannelMessageCommand("WhatsApp", "Nadia", phone, null, "Later.", "wa-3b"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customerId = await db.Customers.Where(c => c.Phone == phone).Select(c => c.Id).SingleAsync();
        var resolvedTicket = await db.Tickets.SingleAsync(t => t.CustomerId == customerId);

        resolvedTicket.ChangeStatus("Open", SystemActors.ChannelIngestion);
        resolvedTicket.ChangeStatus("Resolved", SystemActors.ChannelIngestion);
        await db.SaveChangesAsync();

        var reopened = await SendAsync(new IngestInboundChannelMessageCommand("WhatsApp", "Nadia", phone, null, "New issue.", "wa-3c"));

        reopened.Success.Should().BeTrue();

        var tickets = await db.Tickets.CountAsync(t => t.CustomerId == customerId);
        var messagesOnOldTicket = await db.TicketMessages.CountAsync(m => m.TicketId == resolvedTicket.Id);

        tickets.Should().Be(2, "a terminal ticket must not be reused as the open one");
        messagesOnOldTicket.Should().Be(2, "the new message must not land on the resolved ticket");
    }

    // --- CC-9/CC-12 — a retried provider message id is a no-op, not a duplicate --------------------

    [Fact]
    public async Task CC9_CC12_DuplicateProviderMessageId_IsANoOpReturningTheSameMessage()
    {
        var phone = UniquePhone();
        var command = new IngestInboundChannelMessageCommand("SMS", null, phone, null, "Where is my order?", "sms-123");

        var original = await SendAsync(command);
        var retry = await SendAsync(command);

        original.Success.Should().BeTrue();
        retry.Success.Should().BeTrue();
        retry.Data.Should().Be(original.Data);

        var (_, _, messages) = await CountsAsync("SMS", phone);
        messages.Should().Be(1);
    }

    // --- CC-1 — a web-form (email-based) contact matches an existing email -------------------------

    [Fact]
    public async Task CC1_SecondWebFormContact_MatchesTheEmailCustomer()
    {
        var email = $"web-{Guid.NewGuid():N}@example.com";

        await SendAsync(new IngestInboundChannelMessageCommand("WebForm", "Omar", null, email, "First.", null));
        await SendAsync(new IngestInboundChannelMessageCommand("WebForm", "Omar", null, email, "Second.", null));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customers = await db.Customers.CountAsync(c => c.Email == email);
        var tickets = await db.Tickets.CountAsync(t => db.Customers.Any(c => c.Id == t.CustomerId && c.Email == email));

        customers.Should().Be(1, "the email must match the existing customer, not create a second one");
        tickets.Should().Be(1, "the second message must append to the same ticket");
    }

    // --- CC-4 — payload rules are validated before any write ---------------------------------------

    [Fact]
    public async Task CC4_UnrecognisedChannel_IsRejectedBeforeAnyWrite()
    {
        var phone = UniquePhone();

        var result = await SendAsync(new IngestInboundChannelMessageCommand(
            "Carrier Pigeon", "Nadia", phone, null, "Body.", "wa-x1"));

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Channel");

        var (customers, tickets, messages) = await CountsAsync("Carrier Pigeon", phone);
        customers.Should().Be(0);
        tickets.Should().Be(0);
        messages.Should().Be(0);
    }

    [Fact]
    public async Task CC4_EmptyBody_IsRejectedBeforeAnyWrite()
    {
        var phone = UniquePhone();

        var result = await SendAsync(new IngestInboundChannelMessageCommand(
            "WhatsApp", "Nadia", phone, null, "   ", "wa-x2"));

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Body");

        var (customers, tickets, messages) = await CountsAsync("WhatsApp", phone);
        customers.Should().Be(0);
        tickets.Should().Be(0);
        messages.Should().Be(0);
    }

    [Fact]
    public async Task CC4_NoPhoneAndNoEmail_IsRejectedBeforeAnyWrite()
    {
        var result = await SendAsync(new IngestInboundChannelMessageCommand(
            "SMS", "Nadia", null, null, "Body.", "sms-x1"));

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "CustomerPhone");
        result.Errors.Should().Contain(e => e.Code == SystemCode.ERR005);
    }
}