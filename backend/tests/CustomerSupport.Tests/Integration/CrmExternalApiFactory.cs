extern alias externalapi;

using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Entities.Channels;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Infrastructure.Messaging;
using CustomerSupport.Shared.Contracts.Messages;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// The customer-facing host for the inbound channel webhook tests. Shares the same real LocalDB
/// test database as <see cref="CrmApiFactory"/> — both hosts point at <see cref="TestDatabase"/>,
/// so a WhatsAppGateway configuration row seeded through one factory is visible to the other.
/// This host deliberately never seeds (see its Program.cs); the tests provision their own
/// configuration rows directly, exactly the way the internal host's admin API would.
/// </summary>
public sealed class CrmExternalApiFactory : WebApplicationFactory<externalapi::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", TestDatabase.ConnectionString);
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-at-least-32-characters-long");
        builder.UseSetting("Messaging:Required", "false");
        builder.UseEnvironment("Development");
    }

    /// <summary>Brings the shared test database up to the current migration. See <see cref="TestDatabase"/>.</summary>
    public Task EnsureDatabaseAsync() => TestDatabase.EnsureMigratedAsync();

    /// <summary>Seeds the WhatsAppGateway configuration row the verifier and sender read,
    /// with the app secret stored protected and only restored at the boundary.</summary>
    public Task SeedWhatsAppGatewayAsync(string baseUrl) => GatewayTestData.SeedWhatsAppGatewayAsync(Services, baseUrl);

    /// <summary>
    /// Records an Agent reply on the shared database and delivers it through the real
    /// <see cref="ChatMessagePushedConsumer"/> — the single source of the live-chat push — so the
    /// broadcast reaches the anonymous <c>/hubs/chat</c> session group exactly as an agent message
    /// published on the internal host would after the bus carries it across (CC-30/CC-31).
    /// </summary>
    public async Task<bool> PushAgentMessageAsync(Guid sessionId, string body)
    {
        await using var scope = Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepository<LiveChatMessage>>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var message = LiveChatMessage.Create(sessionId, "Agent", "Agent", null, body);
        await repo.AddAsync(message);
        await uow.SaveChangesAsync();

        await ConsumeAsync(new ChatMessagePushed(
            message.Id, message.SessionId, message.SenderType, message.SenderName,
            message.SenderId, message.Body, message.SentAt));

        return true;
    }

    /// <summary>Drives the real <see cref="ChatMessagePushedConsumer"/> over a real in-process
    /// <see cref="IRealTimeNotifier"/>, so the hub delivery path (and its idempotency guard) is
    /// exercised exactly as it runs on a host with the bus.</summary>
    public async Task ConsumeAsync(ChatMessagePushed message)
    {
        await using var scope = Services.CreateAsyncScope();
        var realtime = scope.ServiceProvider.GetRequiredService<IRealTimeNotifier>();
        var deduplicator = scope.ServiceProvider.GetRequiredService<ChatMessagePushedDeduplicator>();

        var consumer = new ChatMessagePushedConsumer(realtime, deduplicator, NullLogger<ChatMessagePushedConsumer>.Instance);
        var context = new Mock<ConsumeContext<ChatMessagePushed>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        await consumer.Consume(context.Object);
    }
}
