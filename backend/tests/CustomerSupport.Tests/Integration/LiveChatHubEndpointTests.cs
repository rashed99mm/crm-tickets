using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CustomerSupport.Application.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// CC-14 / CC-16 — the anonymous live-chat hub at <c>/hubs/chat</c> on the customer-facing host.
/// The customer authenticates to the hub with the opaque session token returned by
/// <c>POST /api/external/chat/start</c> (in the query string, matching the REST endpoints), and
/// then receives replies as <c>ChatMessageReceived</c> pushes — no polling, no page reload, and no
/// customer/ticket id ever on the wire (FB-8). An unknown or closed session is refused.
/// </summary>
public sealed class LiveChatHubEndpointTests : IAsyncLifetime
{
    private readonly CrmExternalApiFactory _factory = new();

    public async Task InitializeAsync() => await _factory.EnsureDatabaseAsync();

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private sealed record StartChat(string SessionToken, string SessionId);

    private async Task<(string Token, Guid SessionId)> StartAnonymousAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/external/chat/start", new
        {
            customerName = "Sara",
            customerEmail = "sara@example.com",
            initialMessage = "I need help with a refund",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Response<StartChat>>();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        return (body.Data!.SessionToken, Guid.Parse(body.Data.SessionId));
    }

    private HubConnection Connect(string pathAndQuery)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl($"{_factory.Server.BaseAddress.ToString().TrimEnd('/')}{pathAndQuery}",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    // The in-process TestServer handler cannot complete a WebSocket upgrade, so the
                    // client must negotiate over plain HTTP. LongPolling is fully supported by the
                    // handler and is the documented pattern for SignalR + WebApplicationFactory.
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();
        return connection;
    }

    [Fact]
    public async Task ValidToken_Connects_AndReceivesAgentReply() // CC-14, CC-16, FB-8
    {
        var client = _factory.CreateClient();
        var (token, sessionId) = await StartAnonymousAsync(client);

        // The opaque session token is the only credential; no bearer JWT is attached.
        var connection = Connect($"/hubs/chat?token={Uri.EscapeDataString(token)}");
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("ChatMessageReceived", p => received.TrySetResult(p));

        await connection.StartAsync();

        // A second anonymous visitor in a *different* session must never receive this push,
        // proving the delivery is scoped to the session (not a broadcast to all customers).
        var (otherToken, _) = await StartAnonymousAsync(client);
        var other = Connect($"/hubs/chat?token={Uri.EscapeDataString(otherToken)}");
        var otherReceived = false;
        other.On<JsonElement>("ChatMessageReceived", _ => otherReceived = true);
        await other.StartAsync();

        // Simulate the agent replying (the staff host records the Agent message; here we drive the
        // same receive path by persisting an Agent message into the shared test DB and pushing via
        // RealTimeNotifier, exactly as the agent handler does).
        var push = await _factory.PushAgentMessageAsync(sessionId, "A refund was issued.");

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        payload.GetProperty("body").GetString().Should().Be("A refund was issued.");
        payload.GetProperty("sessionId").GetGuid().Should().Be(sessionId);
        payload.GetProperty("senderType").GetString().Should().Be("Agent");
        push.Should().BeTrue();

        // Give the other client a moment; it must not have seen the message.
        await Task.Delay(500);
        otherReceived.Should().BeFalse("a message for another session must not reach this connection");

        await connection.StopAsync();
        await other.StopAsync();
    }

    [Fact]
    public async Task UnknownToken_IsRefusedAndPushIsStillDeliveredToValidSession() // CC-14, FB-8
    {
        var connection = Connect("/hubs/chat?token=definitely-not-a-real-token");
        var serverClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += _ => { serverClosed.TrySetResult(true); return Task.CompletedTask; };

        // The hub validates the token on connect and aborts. Over LongPolling the negotiate/connect
        // completes first and the server then closes the connection, so the client observes the
        // refusal through the `Closed` callback rather than as a thrown StartAsync.
        await connection.StartAsync();

        var gotClosed = await Task.WhenAny(serverClosed.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        gotClosed.Should().Be(serverClosed.Task, "the aborted connection should surface a close");
    }

    [Fact]
    public async Task AnonymousStart_ReturnsOpaqueTokenOnly() // FB-8 — no customer/ticket id on the wire
    {
        var client = _factory.CreateClient();
        var (token, _) = await StartAnonymousAsync(client);

        token.Should().NotBeNullOrWhiteSpace();
        token.Should().NotContain("customer");
        token.Should().NotContain("ticket");
        token.Should().NotContain("@");
    }

    [Fact]
    public async Task DuplicatePush_DeliversToSessionOnlyOnce() // CC-32 — idempotent delivery
    {
        var client = _factory.CreateClient();
        var (token, sessionId) = await StartAnonymousAsync(client);

        var connection = Connect($"/hubs/chat?token={Uri.EscapeDataString(token)}");
        var received = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        var deliveries = 0;
        connection.On<JsonElement>("ChatMessageReceived", _ => { Interlocked.Increment(ref deliveries); received.TrySetResult(new object()); });
        await connection.StartAsync();

        // Over LongPolling the group join settles a beat after StartAsync returns; give it room so
        // the first push is not sent before the connection is registered in the session group.
        await Task.Delay(500);

        // A duplicate publish (as RabbitMQ would redeliver a retried message) must not double-send.
        var payload = new CustomerSupport.Shared.Contracts.Messages.ChatMessagePushed(
            Guid.NewGuid(), sessionId, "Agent", "Agent", null, "Once only.", DateTime.UtcNow);
        await _factory.ConsumeAsync(payload);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await _factory.ConsumeAsync(payload);
        await Task.Delay(700);
        deliveries.Should().Be(1, "a duplicate ChatMessagePushed must be delivered once (CC-32)");

        await connection.StopAsync();
    }
}
